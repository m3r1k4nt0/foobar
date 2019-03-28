//css_include ..\ScriptUtils\ArrangementHelper.cs
//css_include ..\ScriptUtils\ObjectBrowserHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Napa.Core.Geometry;
using Napa.Core.Project;
using Napa.Core.Steel;
using Napa.Hooks.ScriptUtils;
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

                var helper = new ArrangementHelper(so);
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
}