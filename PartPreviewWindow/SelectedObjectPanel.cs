﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget
	{
		private IObject3D item = new Object3D();

		private FlowLayoutWidget editorPanel;
		private TextWidget itemName;
		private ThemeConfig theme;
		private View3DWidget view3DWidget;
		private InteractiveScene scene;

		private Dictionary<Type, HashSet<IObject3DEditor>> objectEditorsByType;

		public SelectedObjectPanel(View3DWidget view3DWidget, InteractiveScene scene, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor |= HAnchor.Left;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = new BorderDouble(8, 10);
			this.MinimumSize = new VectorMath.Vector2(220, 0);

			this.view3DWidget = view3DWidget;
			this.theme = theme;
			this.scene = scene;

			this.AddChild(itemName = new TextWidget("", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				EllipsisIfClipped = true,
				Margin = new BorderDouble(bottom: 10)
			});

			var behavior3DTypeButtons = new FlowLayoutWidget();
			this.AddChild(behavior3DTypeButtons);

			var buttonMargin = new BorderDouble(2, 5);

			// put in the button for making the behavior solid
			var solidButtonView = theme.ButtonFactory.Generate("Color".Localize());
			var solidBehaviorButton = new PopupButton(solidButtonView)
			{
				Name = "Solid Colors",
				AlignToRightEdge = true,
				PopupContent = new ColorSwatchSelector(item, view3DWidget)
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					BackgroundColor = RGBA_Bytes.White
				},
				Margin = buttonMargin
			};
			solidBehaviorButton.Click += (s, e) =>
			{
				item.OutputType = PrintOutputTypes.Solid;
			};

			behavior3DTypeButtons.AddChild(solidBehaviorButton);

			this.AddChild(editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = "editorPanel",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(top: 10),
			});

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			// TODO: Consider only loading once into a static
			var objectEditors = PluginFinder.CreateInstancesOf<IObject3DEditor>();
			foreach (IObject3DEditor editor in objectEditors)
			{
				foreach (Type type in editor.SupportedTypes())
				{
					if (!objectEditorsByType.TryGetValue(type, out mappedEditors))
					{
						mappedEditors = new HashSet<IObject3DEditor>();
						objectEditorsByType.Add(type, mappedEditors);
					}

					mappedEditors.Add(editor);
				}
			}
		}

		public void SetActiveItem(IObject3D selectedItem)
		{
			if (!scene.HasSelection)
			{
				this.Parent.Visible = false;
				return;
			}

			this.itemName.Text = selectedItem.Name ?? selectedItem.GetType().Name;

			this.item = selectedItem;

			this.editorPanel.RemoveAllChildren();

			this.Parent.Visible = true;

			HashSet<IObject3DEditor> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItem.GetType(), out mappedEditors);

			if (mappedEditors == null)
			{
				foreach (var editor in objectEditorsByType)
				{
					if (selectedItem.GetType().IsSubclassOf(editor.Key))
					{
						mappedEditors = editor.Value;
						break;
					}
				}
			}

			// Add any editor mapped to Object3D to the list
			if (objectEditorsByType.TryGetValue(typeof(Object3D), out HashSet<IObject3DEditor> globalEditors))
			{
				foreach (var editor in globalEditors)
				{
					mappedEditors.Add(editor);
				}
			}

			if (mappedEditors != null)
			{
				var dropDownList = new DropDownList("", maxHeight: 300)
				{
					HAnchor = HAnchor.Stretch
				};

				//dropDownList.SelectionChanged += (s, e) =>
				//{
				//	ShowObjectEditor(
				//		mappedEditors.Where(m => m.Name == dropDownList.SelectedLabel).FirstOrDefault());
				//};

				foreach (IObject3DEditor editor in mappedEditors)
				{
					MenuItem menuItem = dropDownList.AddItem(editor.Name);
					menuItem.Selected += (s, e2) =>
					{
						ShowObjectEditor(editor);
					};
				}

				editorPanel.AddChild(dropDownList);

				// Select the active editor or fall back to the first if not found
				var firstEditor = (from editor in mappedEditors
											  let type = editor.GetType()
											  where type.Name == selectedItem.ActiveEditor
											  select editor).FirstOrDefault();

				// Fall back to default editor?
				if (firstEditor == null)
				{
					firstEditor = mappedEditors.First();
				}

				int selectedIndex = 0;
				for (int i = 0; i < dropDownList.MenuItems.Count; i++)
				{
					if (dropDownList.MenuItems[i].Text == firstEditor.Name)
					{
						selectedIndex = i;
						break;
					}
				}

				dropDownList.SelectedIndex = selectedIndex;

				ShowObjectEditor(firstEditor);
			}
		}

		private GuiWidget activeEditorWidget;

		private void ShowObjectEditor(IObject3DEditor editor)
		{
			if (editor == null)
			{
				return;
			}

			activeEditorWidget?.Close();

			var newEditor = editor.Create(scene.SelectedItem, view3DWidget, theme);
			newEditor.HAnchor = HAnchor.Stretch;
			newEditor.VAnchor = VAnchor.Fit;

			editorPanel.AddChild(newEditor);

			activeEditorWidget = newEditor;
		}
	}
}