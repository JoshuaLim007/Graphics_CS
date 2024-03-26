using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Assimp.Metadata;

namespace JLGraphics.Utility
{
    public class SceneHierarchy
    {
        SceneViewManager sceneViewManager1 = null;
        public SceneHierarchy(GuiManager guiManager, SceneViewManager sceneViewManager)
        {
            guiManager.AddWindow("Scene Hierarchy", Update, typeof(SceneHierarchy));
            sceneViewManager1 = sceneViewManager;
        }

        void Update()
        {
            SelectedEntityParents.Clear();
            var selected = GetSelectedEntity();
            if(selected != null)
            {
                var parent = selected.Parent;
                while (parent)
                {
                    SelectedEntityParents.Push(parent);
                    parent = parent.Parent;
                }
            }

            var entities = InternalGlobalScope<Entity>.Values;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Parent != null)
                {
                    continue;
                }
                RenderEntity(entities[i]);
            }
        }
        Entity GetSelectedEntity()
        {
            if(sceneViewManager1.ObjectsSelected.Count == 0)
            {
                return null;
            }
            else
            {
                return sceneViewManager1.ObjectsSelected[0];
            }
        }
        Stack<Entity> SelectedEntityParents = new Stack<Entity>();
        void RenderEntity(Entity entity)  
        {
            ImGui.PushID(entity.InstanceID.ToString());


            bool selected = false;
            if (GetSelectedEntity() == entity)
            {
                selected = true;
            }


            bool opened;
            if (SelectedEntityParents.Count > 0)
            {
                if (entity == SelectedEntityParents.Peek())
                {
                    SelectedEntityParents.Pop();
                    ImGui.SetNextItemOpen(true);
                }
            }

            opened = ImGui.TreeNodeEx(entity.Name, selected ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None);

            if (opened)
            {
                if (ImGui.IsItemFocused())
                {
                    sceneViewManager1.OverrideSelection(entity);
                }
                var children = entity.Children;
                if (children != null)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        RenderEntity(children[i]);
                    }
                }
                ImGui.TreePop();
            }


            ImGui.PopID();
        }
    }
}
