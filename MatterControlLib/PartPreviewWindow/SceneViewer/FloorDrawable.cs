﻿/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class FloorDrawable : IDrawable
	{
		private ISceneContext sceneContext;
		private InteractionLayer.EditorType editorType;
		private ThemeConfig theme;
		private PrinterConfig printer;

		private Color buildVolumeColor;

		private int activeBedHotendClippingImage = -1;

		private ImageBuffer[] bedTextures = null;

		private bool loadingTextures = false;

		public FloorDrawable(InteractionLayer.EditorType editorType, ISceneContext sceneContext, Color buildVolumeColor, ThemeConfig theme)
		{
			this.buildVolumeColor = buildVolumeColor;
			this.sceneContext = sceneContext;
			this.editorType = editorType;
			this.theme = theme;
			this.printer = sceneContext.Printer;

			{
		}

		public bool Enabled { get; set; }

		public string Title { get; } = "Render Floor";

		public string Description { get; } = "Render a plane or bed floor";

		// TODO: Investigate if stage should really change dynamically based on lookingDownOnBed
		public DrawStage DrawStage { get; } = DrawStage.First;

		public bool LookingDownOnBed { get; set; }

		public void Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (editorType == InteractionLayer.EditorType.Printer)
			{
				// only render if we are above the bed
				if (sceneContext.RendererOptions.RenderBed)
				{
					this.UpdateFloorImage(sceneContext.Scene.SelectedItem);

					GLHelper.Render(
						sceneContext.Mesh,
						theme.UnderBedColor,
						RenderTypes.Shaded,
						world.ModelviewMatrix,
						blendTexture: !this.LookingDownOnBed);

					if (sceneContext.PrinterShape != null)
					{
						GLHelper.Render(sceneContext.PrinterShape, theme.BedColor, RenderTypes.Shaded, world.ModelviewMatrix);
					}
				}

				if (sceneContext.BuildVolumeMesh != null && sceneContext.RendererOptions.RenderBuildVolume)
				{
					GLHelper.Render(sceneContext.BuildVolumeMesh, buildVolumeColor, RenderTypes.Shaded, world.ModelviewMatrix);
				}
			}
			else
			{
				int width = 600;

				GL.Disable(EnableCap.Lighting);

				GL.Color4(theme.BedColor);

				GL.Begin(BeginMode.TriangleStrip);
				GL.Vertex3(-width, width, 0);
				GL.Vertex3(width, width, 0);
				GL.Vertex3(-width, -width, 0);

				GL.Vertex3(-width, -width, 0);
				GL.Vertex3(width, width, 0);
				GL.Vertex3(width, -width, 0);
				GL.End();

				GL.Disable(EnableCap.Texture2D);
				GL.Disable(EnableCap.Blend);

				GL.Begin(BeginMode.Lines);
				{
					GL.Color4(theme.BedGridColors.Line);

					for (int i = -width; i <= width; i += 50)
					{
						GL.Vertex3(i, width, 0);
						GL.Vertex3(i, -width, 0);

						GL.Vertex3(width, i, 0);
						GL.Vertex3(-width, i, 0);
					}

					// X axis
					GL.Color4(theme.BedGridColors.Red);
					GL.Vertex3(width, 0, 0);
					GL.Vertex3(-width, 0, 0);

					// Y axis
					GL.Color4(theme.BedGridColors.Green);
					GL.Vertex3(0, width, 0);
					GL.Vertex3(0, -width, 0);

					// Z axis
					GL.Color4(theme.BedGridColors.Blue);
					GL.Vertex3(0, 0, 10);
					GL.Vertex3(0, 0, -10);
				}
				GL.End();
			}
		}

		private void UpdateFloorImage(IObject3D selectedItem)
		{
			// Early exit for invalid cases
			if (loadingTextures
				|| printer == null
				|| printer.Settings.Helpers.NumberOfHotends() != 2
				|| printer.Bed.BedShape != BedShape.Rectangular)
			{
				return;
			}

			if (bedTextures != null)
			{
				int hotendIndex = GetActiveHotendIndex(selectedItem);

				if (activeBedHotendClippingImage != hotendIndex)
				{
					this.SetActiveTexture(bedTextures[hotendIndex + 1]);
					activeBedHotendClippingImage = hotendIndex;
				}
			}
			else
			{
				loadingTextures = true;

				Task.Run(() =>
				{
					var placeHolderImage = new ImageBuffer(5, 5);
					var graphics = placeHolderImage.NewGraphics2D();
					graphics.Clear(theme.BedColor);

					SetActiveTexture(placeHolderImage);

					try
					{
						var bedImage = BedMeshGenerator.CreatePrintBedImage(sceneContext.Printer);

						bedTextures = new[]
						{
							bedImage,					// No limits, basic themed bed
							new ImageBuffer(bedImage),	// T0 limits
							new ImageBuffer(bedImage),	// T1 limits
							new ImageBuffer(bedImage)	// Unioned T0 & T1 limits
						};

						GenerateNozzleLimitsTexture(printer, 0, bedTextures[1]);
						GenerateNozzleLimitsTexture(printer, 1, bedTextures[2]);

						// TODO:
						// GenerateNozzleLimitsTexture(printer, 3, bedTextures[3]);
					}
					catch
					{
					}

					loadingTextures = false;
				});

			}
		}

		private void SetActiveTexture(ImageBuffer bedTexture)
		{
			foreach (var texture in printer.Bed.Mesh.FaceTextures)
			{
				texture.Value.image = bedTexture;
			}

			printer.Bed.Mesh.PropertyBag.Clear();
		}

		private static int GetActiveHotendIndex(IObject3D selectedItem)
		{
			if (selectedItem == null)
			{
				return -1;
			}

			var worldMaterialIndex = selectedItem.WorldMaterialIndex();
			if (worldMaterialIndex == -1)
			{
				worldMaterialIndex = 0;
			}

			return worldMaterialIndex;
		}

		private void GenerateNozzleLimitsTexture(PrinterConfig printer, int hotendIndex, ImageBuffer bedplateImage)
		{
			var xScale = bedplateImage.Width / printer.Settings.BedBounds.Width;
			var yScale = bedplateImage.Height / printer.Settings.BedBounds.Height;

			int alpha = 80;

			var graphics = bedplateImage.NewGraphics2D();

			var hotendBounds = printer.Settings.HotendBounds[hotendIndex];

			// Scale hotendBounds into textures units
			hotendBounds = new RectangleDouble(
				hotendBounds.Left * xScale,
				hotendBounds.Bottom * yScale,
				hotendBounds.Right * xScale,
				hotendBounds.Top * yScale);

			var imageBounds = bedplateImage.GetBounds();

			var dimRegion = new VertexStorage();
			dimRegion.MoveTo(imageBounds.Left, imageBounds.Bottom);
			dimRegion.LineTo(imageBounds.Right, imageBounds.Bottom);
			dimRegion.LineTo(imageBounds.Right, imageBounds.Top);
			dimRegion.LineTo(imageBounds.Left, imageBounds.Top);

			var targetRect = new VertexStorage();
			targetRect.MoveTo(hotendBounds.Right, hotendBounds.Bottom);
			targetRect.LineTo(hotendBounds.Left, hotendBounds.Bottom);
			targetRect.LineTo(hotendBounds.Left, hotendBounds.Top);
			targetRect.LineTo(hotendBounds.Right, hotendBounds.Top);
			targetRect.ClosePolygon();

			var overlayMinusTargetRect = new CombinePaths(dimRegion, targetRect);
			graphics.Render(overlayMinusTargetRect, new Color(Color.Black, alpha));

			string hotendTitle = string.Format("{0} {1}", "Nozzle ".Localize(), hotendIndex + 1);

			var stringPrinter = new TypeFacePrinter(hotendTitle, theme.DefaultFontSize, bold: true);
			var printerBounds = stringPrinter.GetBounds();

			int textPadding = 8;

			var textBounds = printerBounds;
			textBounds.Inflate(textPadding);

			var cornerRect = new RectangleDouble(hotendBounds.Right - textBounds.Width, hotendBounds.Top - textBounds.Height, hotendBounds.Right, hotendBounds.Top);

			graphics.Render(
				new RoundedRectShape(cornerRect, bottomLeftRadius: 6),
				theme.PrimaryAccentColor);

			graphics.DrawString(
				hotendTitle,
				hotendBounds.Right - textPadding,
				cornerRect.Bottom + (cornerRect.Height / 2 - printerBounds.Height / 2) + 1,
				theme.DefaultFontSize,
				justification: Justification.Right,
				baseline: Baseline.Text,
				color: Color.White,
				bold: true);

			graphics.Render(new Stroke(targetRect, 1), theme.PrimaryAccentColor);
		}

		{
		}
	}
}
