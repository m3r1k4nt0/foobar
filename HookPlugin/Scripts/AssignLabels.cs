//css_include ..\ScriptUtils\ArrangementHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Napa.Hooks.ScriptUtils;
using Napa.Scripting;

namespace Napa.Hooks.Scripts {
    public class AssignLabels : ScriptBase {
        public override void Run() {
            var surfaceObjects = Graphics.GetSelectedObjects()
                .Select(d => Geometry.Manager.GetSurfaceObject(d.Name))
                .Where(so => so != null)
                .ToArray();

            foreach (var so in surfaceObjects) {
                var helper = new ArrangementHelper(so);
                helper.AssignLabels(CurrentProjectVersion);
            }
            Napa.Alfred.ModelingWorkspaceVM.PropertyGridViewModel.Update();
        }
    }
}