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