//css_include ..\ScriptUtils\ArrangementHelper.cs
//css_include ..\ScriptUtils\ObjectBrowserHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Napa.Hooks.ScriptUtils;
using Napa.Scripting;

namespace Napa.Hooks.Scripts {
    public class ChangeToPillarObject : ScriptBase {
        public override void Run() {
            var surfaceObjects = Graphics.GetSelectedObjects()
                .Select(d => Geometry.Manager.GetSurfaceObject(d.Name))
                .Where(so => so != null)
                .ToArray();

            var surfaceObjectNames = surfaceObjects
                .Select(so => so.Name)
                .ToArray();

            ObjectBrowserHelper.RemoveItems(CurrentProjectVersion, surfaceObjectNames);
            foreach (var so in surfaceObjects) {
                var helper = new ArrangementHelper(so);
                var path = helper.GetArrangementPath(CurrentProjectVersion);
                var lastArrName = path[path.Length - 1];
                path[path.Length - 1] = "STR*PILL" + lastArrName.Substring(lastArrName.IndexOf('_'));
                try {
                    ObjectBrowserHelper.AddItems(CurrentProjectVersion, path, surfaceObjectNames);
                } catch (Exception e) { }
                helper.AssignStructureType(CurrentProjectVersion, "2-PILLAR");
            }
            var drawables = surfaceObjectNames.Select(n => Napa.Alfred.GraphicsService.DrawableManager.GetFromCache(n)).Where(d => d != null).ToArray();
            Napa.Alfred.GraphicsService.SelectionWorkflow.Activate(drawables);
        }
    }
}
