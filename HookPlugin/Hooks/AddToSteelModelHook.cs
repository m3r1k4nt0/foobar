using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Napa.Core.Geometry;
using Napa.Core.Project;
using Napa.TableProcessing;
using Napa.Gui.ViewModels;
using Napa.Drawables;

namespace Napa.Hooks.Hooks {
    public class AddToSteelModelHook : IHook {

        public void Run(IProjectVersion version, string name) {

            try {
                var so = version.GeometryManager.GetSurfaceObject(name);
                if (so == null)
                    return;

                var helper = new ArragementHelper(so);
                var path = helper.GetArrangementPath(version);
                ObjectBrowserHelper.AddItems(version, path, new[] { name });
                helper.AssingStructureType(version);
                helper.AssignLabels(version);

            } catch (Exception e) {
                Console.WriteLine(e.StackTrace);
                throw e;
            }
        }
    }

    public class ArragementHelper {
        
        public ISurfaceObject SurfaceObject { get; private set; }

        public ArragementHelper(ISurfaceObject so) {
            SurfaceObject = so;
        }

        public bool AssingStructureType(IProjectVersion version) {
            var dMgr = Alfred.GraphicsService.DrawableManager.GetFromCache(name);
            var geom = dMgr.GetFromCache(name) as CompositeGeometry;
            if (geom == null)
                return;
            geom.StructureType = GetStructureType();
        }

        public bool AssignLabels(IProjectVersion version) {
            var mvz = GetMainVerticalZone(version);
            var dz = GetDeckZone(version, mvz);

            var currentLabels = Napa.Core.Geometry.Labeling.GetLabels(SurfaceObject.Name);
            var labels = new[] {
                mvz.Name,
                "DECK_" + dz.ID,
                GetGenericStructureType()
            }.Concat(currentLabels)
            .Distinct();

            Napa.Core.Geometry.Labeling.SetLabels(SurfaceObject.Name, labels);
            return true;
        }

        public string GetArrangementName(MainVerticalZone mvz, DeckZone dz) {
            var str = GetGenericStructureType();
            return "STR*" + str + "_" + mvz.Index + "_" + dz.ID;
        }

        public string[] GetArrangementPath(IProjectVersion version) {
            var mvz = GetMainVerticalZone(version);
            var deckZone = GetDeckZone(version, mvz);
            return new String[] {
                "STR*STEEL",
                mvz.GetArrangementName(),
                deckZone.GetArrangementName(mvz),
                GetArrangementName(mvz,deckZone)
            };
        }

        private IHierarchicalArrangement GetArrangement(IProjectVersion version) {
            var mvz = GetMainVerticalZone(version);
            if (mvz == null)
                return null;

            var deckZone = GetDeckZone(version, mvz);
            if (deckZone == null)
                return null;

            return GetLeafArrangement(version, mvz, deckZone);
        }


        private IHierarchicalArrangement GetLeafArrangement(IProjectVersion version, MainVerticalZone mvz, DeckZone dz) {
            var name = GetArrangementName(mvz, dz);
            var deckArr = dz.GetArrangement(version, mvz);
            var leafArr = deckArr.SubArrangements.FirstOrDefault(arr => arr.Name == name);
            if (leafArr == null)
                leafArr = deckArr.AddSubArrangement(name);
            return leafArr;
        }

        private string GetStructureType() {
            return GetGenericStructureType() == "TBH" ? "1-TBH_1" : "0-LBH_1";
        }

        private string GetGenericStructureType() {
            var b = SurfaceObject.BoundingBox;
            return b.YLength > b.XLength ? "TBH" : "LBH";
        }

        private DeckZone GetDeckZone(IProjectVersion version, MainVerticalZone mvz) {
            var p = SurfaceObject.CenterOfGravity;
            return DeckZone.GetDefinedDeckZones(version)
                .Select(z => {
                    var s = version.GeometryManager.GetSurface(z.Surface);
                    var pClosest = s.GetClosestPoint(p);
                    return new { Zone = z, Point = pClosest };
                }).OrderBy(item => item.Point.GetDistance(p))
                .Take(2)
                .OrderByDescending(item => item.Point.Z)
                .Select(item => item.Zone)
                .FirstOrDefault();
        }

        private MainVerticalZone GetMainVerticalZone(IProjectVersion version) {
            var x = SurfaceObject.CenterOfGravity.X;
            return MainVerticalZone
                .GetMainVerticalZones(version)
                .FirstOrDefault(z => z.Range.IsIncluding(x, 0.01));
        }
    }

    public static class ObjectBrowserHelper {

        public static NapaArrangementBrowserViewModel BrowserVM {
            get {
                return Napa.Alfred.MainWindowVM.ArrangementBrowserVM.SteelObjectBrowserViewModel;
            }
        }
       
        public static void AddItems(IProjectVersion version, IEnumerable<string> path, string[] items) {
            var vm = BrowserVM;
            var node = GetNode(version, vm, vm.ActualRoot, path);
            AddDrawablesToNode(vm, node, items);
        }

        private static ObjectBrowserNodeViewModel GetNode(IProjectVersion version, NapaArrangementBrowserViewModel vm,
                ObjectBrowserNodeViewModel root, IEnumerable<string> path) {

            if (!path.Any())
                return root;

            var children = GetChildren(root);
            ObjectBrowserItemViewModel node;
            if (children.TryGetValue(path.First(), out node))
                return GetNode(version, vm, node as ObjectBrowserNodeViewModel, path.Skip(1));

            var arr = version.GetArrangement(root.Name);
            Napa.Alfred.EventProxy.WithDelayedEvents(() => {
                Napa.Alfred.EventProxy.WithDelayedGMEvents(() => {
                    var subArr = GetSubArr(version, arr, path.First());
                    if (subArr == null)
                        throw new InvalidOperationException("Cannot create STR path " + string.Join(":", path));
                    PopulateArrangement(vm, subArr, root);
                });
            });
            return GetNode(version, vm, root, path);
        }

        private static void AddDrawablesToNode(NapaArrangementBrowserViewModel vm,
                ObjectBrowserNodeViewModel nodeViewModel, string[] names) {

            var method = vm.GetType().GetMethod("AddDrawablesToNode", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(vm, new object[] { nodeViewModel, names });
        }

        private static IHierarchicalArrangement GetSubArr(IProjectVersion version,
                    IHierarchicalArrangement parent, string name) {

            var subArr = version.GetArrangement(name);
            if (subArr == null)
                subArr = parent.AddSubArrangement(name);
            return subArr;
        }

        private static void PopulateArrangement(NapaArrangementBrowserViewModel vm,
                IHierarchicalArrangement arrangement, ObjectBrowserNodeViewModel parent) {
            var method = vm.GetType().GetMethod("PopulateArrangement", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(vm, new object[] { arrangement, parent });
        }


        private static Dictionary<string, ObjectBrowserItemViewModel> GetChildren(ObjectBrowserItemViewModel vm) {
            var property = vm.GetType().GetProperty("Children", BindingFlags.NonPublic | BindingFlags.Instance);
            return property.GetValue(vm) as Dictionary<string, ObjectBrowserItemViewModel>;
        }
    }

    public class MainVerticalZone {

        public string Name { get; set; }

        public ShipCoordinate Min { get; set; }

        public ShipCoordinate Max { get; set; }

        public string Index { get; set; }

        public Limits Range {
            get {
                return new Limits(Min.DoubleValue, Max.DoubleValue);
            }
        }

        public IHierarchicalArrangement GetArrangement(IProjectVersion version) {
            var mvzArrName = GetArrangementName();
            var mvzArr = version.GetArrangement(mvzArrName);
            if (mvzArr == null) {
                var strSteel = version.GetArrangement("STR*STEEL");
                mvzArr = strSteel.AddSubArrangement(mvzArrName);
            }
            return mvzArr;
        }

        public string GetArrangementName() {
            return "STR*" + Name;
        }

        public override string ToString() {
            return Name + " " + Min.StringValue + ":" + Max.StringValue;
        }

        public static IEnumerable<MainVerticalZone> GetMainVerticalZones(IProjectVersion version) {
            var name = "TAB*MVZ";
            var t = version.GetTable(name);
            if (t == null)
                return Enumerable.Empty<MainVerticalZone>();

            return t.Rows.Cast<IRow>()
                .Select(r => new MainVerticalZone() {
                    Name = r.GetStringValue("NAME"),
                    Min = ShipCoordinate.Create(r.GetStringValue("LLIMIT")),
                    Max = ShipCoordinate.Create(r.GetStringValue("ULIMIT")),
                    Index = r.GetStringValue("NR")
                })
                .OrderByDescending(mzv => mzv.Name)
                .ToList();
        }
    }

    public class DeckZone {

        public string ID { get; set; }

        public string Surface { get; set; }

        public IHierarchicalArrangement GetArrangement(IProjectVersion version, MainVerticalZone mvz) {

            var name = GetArrangementName(mvz);
            var mvzArr = mvz.GetArrangement(version);
            var deckArr = mvzArr.SubArrangements.FirstOrDefault(arr => arr.Name == name);
            if (deckArr == null)
                deckArr = mvzArr.AddSubArrangement(name);
            return deckArr;
        }

        public string GetArrangementName(MainVerticalZone mvz) {
            return "STR*DECK_" + mvz.Index + "_" + ID;
        }

        public override string ToString() {
            return ID + " " + Surface;
        }

        public static IEnumerable<DeckZone> GetDefinedDeckZones(IProjectVersion version) {
            var name = "TAB*DECKS";
            var t = version.GetTable(name);
            if (t == null)
                return Enumerable.Empty<DeckZone>();

            return t.Rows
                .Cast<IRow>()
                .Select(r => new DeckZone() {
                    ID = r.GetStringValue("NAME"),
                    Surface = r.GetStringValue("SURFACE")
                })
                .ToList();
        }
    }
}