using UnityEditor;
using UnityEngine;
using Varneon.VUdon.Editors.Editor;

namespace Varneon.VUdon.Playerlist.Editor
{
    [CustomEditor(typeof(Playerlist))]
    public class PlayerlistEditor : InspectorBase
    {
        [SerializeField]
        private Texture2D headerIcon;

        protected override string PersistenceKey => "Varneon/VUdon/Playerlist/Editor/Foldout";

        protected override InspectorHeader Header => new InspectorHeaderBuilder()
            .WithTitle("VUdon - Playerlist")
            .WithDescription("UI for displaying information about players in the instance")
            .WithURL("GitHub", "https://github.com/Varneon")
            .WithIcon(headerIcon)
            .Build();
    }
}
