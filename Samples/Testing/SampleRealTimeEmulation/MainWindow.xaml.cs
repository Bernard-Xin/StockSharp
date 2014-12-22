namespace SampleRealTimeEmulation
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Windows;

	using Ecng.Common;
	using Ecng.Collections;
	using Ecng.Xaml;

	using StockSharp.Algo;
	using StockSharp.Algo.Candles;
	using StockSharp.Algo.Testing;
	using StockSharp.BusinessEntities;
	using StockSharp.Logging;
	using StockSharp.Messages;
	using StockSharp.SmartCom;
	using StockSharp.Xaml;
	using StockSharp.Xaml.Charting;
	using StockSharp.Localization;

	public partial class MainWindow
	{
		private bool _isConnected;
		private CandleManager _candleManager;
		private RealTimeEmulationTrader<Connector> _connector;
		private readonly ChartCandleElement _candlesElem;
		private readonly LogManager _logManager;
		private Security _security;
		private readonly SynchronizedList<Candle> _buffer = new SynchronizedList<Candle>(); 

		public MainWindow()
		{
			InitializeComponent();

			_logManager = new LogManager();
			_logManager.Listeners.Add(new GuiLogListener(Log));

			var area = new ChartArea();
			Chart.Areas.Add(area);

			_candlesElem = new ChartCandleElement();
			area.Elements.Add(_candlesElem);

			GuiDispatcher.GlobalDispatcher.AddPeriodicalAction(ProcessCandles);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (_connector != null)
				_connector.Dispose();

			base.OnClosing(e);
		}

		private void ConnectClick(object sender, RoutedEventArgs e)
		{
			if (!_isConnected)
			{
				if (_connector == null)
				{
					if (Login.Text.IsEmpty())
					{
						MessageBox.Show(this, LocalizedStrings.Str2974);
						return;
					}
					else if (Password.Password.IsEmpty())
					{
						MessageBox.Show(this, LocalizedStrings.Str2975);
						return;
					}
					
					// создаем подключение
					_connector = new RealTimeEmulationTrader<Connector>(new SmartTrader
					{
						Login = Login.Text,
						Password = Password.Password,
						Address = Address.SelectedAddress
					});

					//_trader = new RealTimeEmulationTrader<Connector>(new StockSharp.Plaza.PlazaTrader
					//{
					//	IsCGate = true,
					//}, portfolio);

					SecurityEditor.SecurityProvider = new FilterableSecurityProvider(_connector);

					_logManager.Sources.Add(_connector);
					
					// очищаем из текстового поля в целях безопасности
					//Password.Clear();

					// подписываемся на событие успешного соединения
					_connector.Connected += () =>
					{
						// возводим флаг, что соединение установлено
						_isConnected = true;

						// разблокируем кнопку Экспорт
						this.GuiAsync(() => ChangeConnectStatus(true));

						_candleManager = new CandleManager(_connector);

						_connector.NewMarketDepths += OnDepths;
						_connector.MarketDepthsChanged += OnDepths;

						_connector.NewOrders += orders => Orders.Orders.AddRange(orders);
						_connector.NewMyTrades += trades => Trades.Trades.AddRange(trades);

						// подписываемся на событие о неудачной регистрации заявок
						_connector.OrdersRegisterFailed += OrdersFailed;

						_candleManager.Processing += (s, candle) =>
						{
							if (candle.State == CandleStates.Finished)
								_buffer.Add(candle);
						};

						_connector.StartExport();

						this.GuiAsync(() =>
						{
							ConnectBtn.IsEnabled = false;
						});
					};

					// подписываемся на событие разрыва соединения
					_connector.ConnectionError += error => this.GuiAsync(() =>
					{
						// заблокируем кнопку Экспорт (так как соединение было потеряно)
						ChangeConnectStatus(false);

						MessageBox.Show(this, error.ToString(), LocalizedStrings.Str2959);
					});

					// подписываемся на ошибку обработки данных (транзакций и маркет)
					_connector.ProcessDataError += error =>
						this.GuiAsync(() => MessageBox.Show(this, error.ToString(), LocalizedStrings.Str2955));

					// подписываемся на ошибку подписки маркет-данных
					_connector.MarketDataSubscriptionFailed += (security, type, error) =>
						this.GuiAsync(() => MessageBox.Show(this, error.ToString(), LocalizedStrings.Str2956Params.Put(type, security)));
				}

				_connector.Connect();
			}
			else
			{
				_connector.Disconnect();
			}
		}

		private void OnDepths(IEnumerable<MarketDepth> depths)
		{
			if (_security == null)
				return;

			var depth = depths.FirstOrDefault(d => d.Security == _security);

			if (depth == null)
				return;

			DepthControl.UpdateDepth(depth);
		}

		private void OrdersFailed(IEnumerable<OrderFail> fails)
		{
			this.GuiAsync(() =>
			{
				foreach (var fail in fails)
					MessageBox.Show(this, fail.Error.ToString(), LocalizedStrings.Str2960);
			});
		}

		private void ChangeConnectStatus(bool isConnected)
		{
			_isConnected = isConnected;
			ConnectBtn.Content = isConnected ? LocalizedStrings.Str2961 : LocalizedStrings.Str2962;
		}

		private void ProcessCandles()
		{
			foreach (var candle in _buffer.SyncGet(c => c.CopyAndClear()))
				Chart.Draw(_candlesElem, candle);
		}

		private void SecurityEditor_OnSecuritySelected()
		{
			_security = SecurityEditor.SelectedSecurity;

			Chart.Reset(new[] { _candlesElem });

			_connector.RegisterMarketDepth(_security);
			_connector.RegisterTrades(_security);

			_candleManager.Start(new CandleSeries(typeof(TimeFrameCandle), _security, TimeSpan.FromMinutes(1)));
		}

		private void NewOrder_OnClick(object sender, RoutedEventArgs e)
		{
			var newOrder = new OrderWindow
			{
				Order = new Order { Security = _security },
				Connector = _connector,
			};

			if (newOrder.ShowModal(this))
				_connector.RegisterOrder(newOrder.Order);
		}
	}
}