using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Napa.Core.Project;
using Napa.Drawables;
using Napa.Gui.ViewModels;
using Napa.TableProcessing;

namespace Napa.Hooks.ScriptUtils {

    public static class ObjectBrowserHelper {

        public static NapaArrangementBrowserViewModel BrowserVM {
            get {
                return Napa.Alfred.ModelingWorkspaceVM.ArrangementBrowserVM.SteelObjectBrowserViewModel;
            }
        }

        public static void AddItems(IProjectVersion version, IEnumerable<string> path, string[] items) {
            var vm = BrowserVM;
            var node = GetNode(version, vm, vm.ActualRoot, path);
            AddDrawablesToNode(vm, node, items);
        }

        public static void RemoveItems(IProjectVersion version, string[] items) {
            foreach (var name in items) {
                TearDownComposite(name);
                var steelArr = version.DefaultStructureArrangement;
                var subArrs = steelArr.ElementaryArrangements;
                var arr = subArrs.FirstOrDefault(a => a.Names.Contains(name));
                if (arr == null) continue;
                arr.Remove(name);

                var itemVM = FindItemByName(BrowserVM.ActualRoot, name);
                if (itemVM == null) continue;
                var nodeVM = itemVM.Parent;
                var method = nodeVM.GetType().GetMethod("RemoveChild", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(nodeVM, new object[] { itemVM });
                ((ObjectBrowserNodeViewModel)nodeVM).NotifyCountChanged();
            }
        }

        private static void TearDownComposite(string name) {
            var drawableManager = Napa.Alfred.GraphicsService.DrawableManager;
            var drawable = drawableManager.Get(name) as IMainGeometry;
            if (!(drawable is CompositeGeometry)) return;

            var composite = drawable as CompositeGeometry;
            var visible = composite.Visible;
            drawableManager.DisposeDrawable(composite);
            var geometry = drawableManager.Get(name) as Napa.Graphics.Drawables.IGeometry;
            if (geometry != null && visible) geometry.Show();
        }

        private static ObjectBrowserItemViewModel FindItemByName(ObjectBrowserItemViewModel item, string name) {
            var res1 = GetChildByName(item, name);
            if (res1 != null) return res1;

            var children = GetChildren(item);
            foreach (var i in children) {
                var res2 = FindItemByName(i.Value, name);
                if (res2 != null) return res2;
            }
            return null;
        }

        private static ObjectBrowserItemViewModel GetChildByName(ObjectBrowserItemViewModel item, string name) {
            var children = GetChildren(item);
            ObjectBrowserItemViewModel child;
            children.TryGetValue(name, out child);
            return child;
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
}