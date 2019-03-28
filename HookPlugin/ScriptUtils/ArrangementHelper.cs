using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Napa;
using Napa.Core.Geometry;
using Napa.Core.Project;
using Napa.Drawables;
using Napa.TableProcessing;

namespace Napa.Hooks.ScriptUtils {

    public class ArrangementHelper {

        private static readonly Regex REF_OBJECT_REGEX
            = new Regex(@"^REF\s*,?\s+(?<NAME>[^\n\s,;]+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public ISurfaceObject SurfaceObject { get; private set; }

        public ArrangementHelper(ISurfaceObject so) {
            SurfaceObject = so;
        }

        public bool AssingStructureType(IProjectVersion version) {
            var dMgr = Alfred.GraphicsService.DrawableManager;
            var geom = dMgr.GetFromCache(SurfaceObject.Name) as CompositeGeometry;
            if (geom == null) return false;

            string structureType = null;
            string originalObjectName;
            if (IsReflectedObject(out originalObjectName)) {
                var mainObject = version.SteelManager.DefaultModel.GetMainObjectByName(originalObjectName);
                if (mainObject != null && mainObject.StructureType != null)
                    structureType = mainObject.StructureType.Name;
            }

            geom.StructureType = structureType ?? GetStructureType();
            return true;
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

        private string GetArrangementName(MainVerticalZone mvz, DeckZone dz) {
            var str = GetGenericStructureType();
            return "STR*" + str + "_" + mvz.Index + "_" + dz.ID;
        }

        public string[] GetArrangementPath(IProjectVersion version) {
            var mvz = GetMainVerticalZone(version);
            var deckZone = GetDeckZone(version, mvz);
            return new[] {
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
            var genericStructureType = GetGenericStructureType();
            if (genericStructureType == "TBH") return "1-TBH_1";
            if (genericStructureType == "LBH") return "0-LBH_1";
            return "3-DECK_1";
        }

        private string GetGenericStructureType() {
            var orientation = SurfaceObject.BoundingBox.GetMinDimensionAxis();
            if (orientation == Axis.X) return "TBH";
            if (orientation == Axis.Y) return "LBH";
            return "DK";
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

        private bool IsReflectedObject(out string originalObjectName) {
            originalObjectName = null;
            var match = REF_OBJECT_REGEX.Match(SurfaceObject.Definition);
            if (!match.Success) return false;
            originalObjectName = match.Groups["NAME"].ToString().Trim();
            return true;
        }

        private class MainVerticalZone {

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
                    .OrderByDescending(mvz => mvz.Name)
                    .ToList();
            }
        }

        private class DeckZone {

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
}