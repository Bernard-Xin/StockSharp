﻿namespace StockSharp.Studio.Controls
{
	using System;
	using System.Windows;
	using System.Windows.Controls;

	using Ecng.Configuration;
	using Ecng.Xaml;

	using StockSharp.Studio.Core;
	using StockSharp.Studio.Core.Commands;

	public class MarketDataSettingsComboBox : ComboBox
	{
		private MarketDataSettingsCache _cache;

		public static readonly DependencyProperty SelectedSettingsProperty = DependencyProperty.Register("SelectedSettings", typeof(MarketDataSettings), typeof(MarketDataSettingsComboBox),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedSettingsPropertyChangedCallback));

		private static void SelectedSettingsPropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
		{
			var ctrl = (MarketDataSettingsComboBox)sender;
			var settings = (MarketDataSettings)args.NewValue;

			ctrl.SettingsChanged(settings);
		}

		public MarketDataSettings SelectedSettings
		{
			get { return (MarketDataSettings)GetValue(SelectedSettingsProperty); }
			set { SetValue(SelectedSettingsProperty, value); }
		}

		public MarketDataSettingsComboBox()
		{
			_cache = ConfigManager.TryGetService<MarketDataSettingsCache>();

			if (_cache == null)
			{
				ConfigManager.ServiceRegistered += (t, s) =>
				{
					if (typeof(MarketDataSettingsCache) != t)
						return;

					_cache = (MarketDataSettingsCache)s;
					GuiDispatcher.GlobalDispatcher.AddAction(() => ItemsSource = _cache.Settings);
				};
			}
			else
				ItemsSource = _cache.Settings;

			DisplayMemberPath = "Path";

			SelectionChanged += MarketDataSettingsComboBoxSelectionChanged;
		}

		private void MarketDataSettingsComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var item = (MarketDataSettings)SelectedItem;

			var isNew = item == _cache.NewSettingsItem;
			var selection = !isNew
				? item
				: new MarketDataSettings
				{
					UseLocal = true,
					//IsAlphabetic = true,
					Path = Environment.CurrentDirectory
				};

			SelectedSettings = selection;

			if (!isNew)
				return;

			_cache.Settings.Add(selection);
			new OpenMarketDataSettingsCommand(selection).Process(this);
		}

		private void SettingsChanged(MarketDataSettings settings)
		{
			SelectedItem = settings;
		}
	}
}
