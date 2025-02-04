﻿/*
Copyright (c) 2018, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class ConfigurePrinterWidget : FlowLayoutWidget, ICloseableTab
	{
		public ConfigurePrinterWidget(SettingsContext settingsContext, PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			var inlineNameEdit = new InlineStringEdit(printer.PrinterName, theme, "Printer Name", boldFont: true);
			inlineNameEdit.ValueChanged += (s, e) =>
			{
				printer.Settings.SetValue(SettingsKey.printer_name, inlineNameEdit.Text);
			};
			this.AddChild(inlineNameEdit);

			void Printer_SettingChanged(object s, StringEventArgs stringEvent)
			{
				if (s is PrinterSettings printerSettings
					&& stringEvent?.Data == SettingsKey.printer_name)
				{
					// Try to find a printer tab for the given printer
					inlineNameEdit.Text = printer.PrinterName;
				}
			}

			printer.Settings.SettingChanged += Printer_SettingChanged;

			inlineNameEdit.Closed += (s, e) =>
			{
				printer.Settings.SettingChanged -= Printer_SettingChanged;
			};

			var settingsSection = PrinterSettings.Layout.PrinterSections[0];
			switch (UserSettings.Instance.get(UserSettingsKey.SliceSettingsViewDetail))
			{
				case "Simple":
					settingsSection = PrinterSettings.Layout.PrinterSections[0];
					break;

				case "Intermediate":
					settingsSection = PrinterSettings.Layout.PrinterSections[1];
					break;

				case "Advanced":
					settingsSection = PrinterSettings.Layout.PrinterSections[2];
					break;
			}

			this.AddChild(
				new SliceSettingsTabView(
					settingsContext,
					"ConfigurePrinter",
					printer,
					settingsSection,
					theme,
					isPrimarySettingsView: true,
					justMySettingsTitle: "My Modified Settings (Printer)".Localize(),
					databaseMRUKey: UserSettingsKey.ConfigurePrinter_CurrentTab));
		}
	}
}
