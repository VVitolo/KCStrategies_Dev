#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Core;
using BlueZ = NinjaTrader.NinjaScript.Indicators.BlueZ; // Alias for better readability
using RegressionChannel = NinjaTrader.NinjaScript.Indicators.RegressionChannel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    abstract public class KCAlgoBase : Strategy, ICustomTypeDescriptor
    {
        #region Variables
		
        private DateTime lastEntryTime;
        private readonly TimeSpan tradeDelay = TimeSpan.FromSeconds(10);	
		
        // Dictionary to track messages printed by PrintOnce (Key = message key, Value = bar number printed)
        private Dictionary<string, int> printedMessages = new Dictionary<string, int>();
		private bool marketIsChoppy;
		private bool autoDisabledByChop; // Tracks if Auto was turned off by the system due to chop

        // Indicator Variables
        private BlueZ.BlueZHMAHooks hullMAHooks;
        private bool hmaUp;
        private bool hmaDown;

        private BuySellPressure BuySellPressure1;
        private bool buyPressureUp;
        private bool sellPressureUp;
		private Series<double> buyPressure;
		private Series<double> sellPressure;		

        private RegressionChannel RegressionChannel1, RegressionChannel2;
        private RegressionChannelHighLow RegressionChannelHighLow1;
        private bool regChanUp;
        private bool regChanDown;

        private VMA VMA1;
        private bool volMaUp;
        private bool volMaDown;

        private NTSvePivots pivots;
        private double pivotPoint, s1, s2, s3, r1, r2, r3, s1m, s2m, s3m, r1m, r2m, r3m;

		private Momentum Momentum1;
		private double currentMomentum;		
        private bool momoUp;
        private bool momoDown;
		
        private ADX ADX1;
		private double currentAdx;
        private bool adxUp;

        private ATR ATR1;
		private double currentAtr;
        private bool atrUp;

        private bool aboveEMAHigh;
        private bool belowEMALow;

        private bool uptrend;
        private bool downtrend;

        private bool priceUp;
        private bool priceDown;

        public bool isLong;
        public bool isShort;
        public bool isFlat;
        public bool exitLong;
        public bool exitShort;
        public bool longSignal;
        public bool shortSignal;

        private double lastStopLevel = 0;  // Tracks the last stop level
        private bool stopUpdated = false;  // To ensure stop is moved only when favorable

        // Progress tracking
        private double actualPnL;
        private int trailStop;
        private bool _beRealized;
        private bool enableFixedStopLoss = false;
        private bool threeStepTrail;
        private bool trailingDrawdownReached = false;
        private int ProgressState;

        private double entryPrice;
        private double currentPrice;
        private bool additionalContractExists;

        private bool isBuySellMarketOrder;
        private bool tradesPerDirection;
        private int counterLong;
        private int counterShort;
        private bool QuickLong;
        private bool QuickShort;
        private bool quickLongBtnActive;
        private bool quickShortBtnActive;

        //		private bool isEnableTime1;
        private bool isEnableTime2;
        private bool isEnableTime3;
        private bool isEnableTime4;
        private bool isEnableTime5;
        private bool isEnableTime6;

        private bool isManualEnabled;
        private bool isAutoEnabled;
        private bool isLongEnabled;
        private bool isShortEnabled;

        //		Chart Trader Buttons
        private System.Windows.Controls.RowDefinition addedRow;
        private Gui.Chart.ChartTab chartTab;
        private Gui.Chart.Chart chartWindow;
        private System.Windows.Controls.Grid chartTraderGrid, chartTraderButtonsGrid, lowerButtonsGrid;
		
        //		New Toggle Buttons
        private System.Windows.Controls.Button manualBtn, autoBtn, longBtn, shortBtn, quickLongBtn, quickShortBtn;
        private System.Windows.Controls.Button add1Btn, close1Btn, BEBtn, TSBtn, moveTSBtn, moveToBEBtn;
        private System.Windows.Controls.Button moveTS50PctBtn, closeBtn, panicBtn, donatePayPalBtn;
        private bool panelActive;
        private System.Windows.Controls.TabItem tabItem;
        private System.Windows.Controls.Grid myGrid;

        // KillAll
        private Account chartTraderAccount;
        private AccountSelector accountSelector;
        private Order myEntryOrder = null;
        private Order myStopOrder = null;
        private Order myTargetOrder = null;
        private double myStopPrice = 0;
        private double myLimitPrice = 0;
		private bool activeOrder;

        //		Status Panel
        private string textLine0;
        private string textLine1;
        private string textLine2;
        private string textLine3;
        private string textLine4;
        private string textLine5;
        private string textLine6;
        private string textLine7;

        //		PnL
        private double totalPnL;
        private double cumPnL;
        private double dailyPnL;
        private bool canTradeOK = true;

        private bool syncPnl;
        private double historicalTimeTrades;//Sync  PnL
        private double dif;//To Calculate PNL sync
        private double cumProfit;//For real time pnl and pnl synchronization

        private bool restartPnL;

        private bool beSetAuto;
        private bool showctrlBESetAuto;
        private bool atrTrailSetAuto;
        private bool showAtrTrailSetAuto;
        private bool enableTrail;
        private bool showTrailOptions;
        public bool tickTrail;

        private TrailStopTypeKC trailStopType;
        private bool showTickTrailOption;
        private bool showAtrTrailOptions;
        private bool showThreeStepTrailOptions;

        private bool enableFixedProfitTarget;		
        private bool enableRegChanProfitTarget;
        private bool enableDynamicProfitTarget;
		
        // Error Handling
        private readonly object orderLock = new object(); // Critical for thread safety
        private Dictionary<string, Order> activeOrders = new Dictionary<string, Order>(); // Track active orders with labels.
        private DateTime lastOrderActionTime = DateTime.MinValue;
        private readonly TimeSpan minOrderActionInterval = TimeSpan.FromSeconds(1); // Prevent rapid order submissions.
        private bool orderErrorOccurred = false; // Flag to halt trading after an order error.

        // Rogue Order Detection
        private DateTime lastAccountReconciliationTime = DateTime.MinValue;
        private readonly TimeSpan accountReconciliationInterval = TimeSpan.FromMinutes(5); // Check for rogue orders every 5 minutes

        // Trailing Drawdown variables
        private double maxProfit;  // Stores the highest profit achieved

        #endregion

        #region Order Label Constants (Highly Recommended)

        // Define your order labels as constants.  This prevents typos and ensures consistency.
        private const string LE = "LE";
		private const string LE2 = "LE2";
		private const string LE3 = "LE3";
		private const string LE4 = "LE4";
        private const string SE = "SE";
        private const string SE2 = "SE2";
        private const string SE3 = "SE3";
        private const string SE4 = "SE4";
        private const string QLE = "QLE";
        private const string QSE = "QSE";
		private const string Add1LE = "Add1LE";
		private const string Add1SE = "Add1SE";
        private const string ManualClose1 = "Manual Close 1"; // Label for the manual close action
        // Add constants for other order labels as needed (e.g., LE2, SE2, "TrailingStop")

        #endregion

		#region Constants

		private const string ManualButton = "ManualBtn";
		private const string AutoButton = "AutoBtn";
		private const string LongButton = "LongBtn";
		private const string ShortButton = "ShortBtn";
		private const string QuickLongButton = "QuickLongBtn";
		private const string QuickShortButton = "QuickShortBtn";
		private const string Add1Button = "Add1Btn";
		private const string Close1Button = "Close1Btn";
		private const string BEButton = "BEBtn";
		private const string TSButton = "TSBtn";
		private const string MoveTSButton = "MoveTSBtn";
		private const string MoveTS50PctButton = "MoveTS50PctBtn";
		private const string MoveToBeButton = "MoveToBeBtn";
		private const string CloseButton = "CloseBtn";
		private const string PanicButton = "PanicBtn";		
		private const string DonatePayPalButton = "BuyCoffeeBtn";

		#endregion
		
        #region TradeToDiscord

        private ClientWebSocket clientWebSocket;
        private List<dynamic> signalHistory = new List<dynamic>();
        private DateTime lastDiscordMessageTime = DateTime.MinValue;
        private readonly TimeSpan discordRateLimitInterval = TimeSpan.FromSeconds(30); // Adjust the interval as needed

        private string lastSignalType = "N/A";
        private double lastEntryPrice = 0.0;
        private double lastStopLoss = 0.0;
        private double lastProfitTarget = 0.0;
        private DateTime lastSignalTime = DateTime.MinValue;

        #endregion

        public override string DisplayName { get { return Name; } }

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
				Description									= @"Base Strategy with OEB v.5.0.2 TradeSaber(Dre). and ArchReactor for KC (Khanh Nguyen)";
				Name										= "KCAlgoBase";
				BaseAlgoVersion								= "KCAlgoBase v5.4";
				Author										= "indiVGA, Khanh Nguyen, Oshi, based on ArchReactor";
				Version										= "Version 5.4 Apr. 2025";
				Credits										= "";
				StrategyName 								= "";
				ChartType									= "Orenko 34-40-40";	
				paypal 										= "https://www.paypal.com/signin"; 
		

                EntriesPerDirection = 10;					// This value should limit the number of contracts that the strategy can open per direction.
															// It has nothing to do with the parameter defining the entries per direction that we define in the strategy and are controlled by code.
                Calculate									= Calculate.OnEachTick;
				EntryHandling 								= EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy 				= true;
                ExitOnSessionCloseSeconds 					= 30;
                IsFillLimitOnTouch 							= false;
                MaximumBarsLookBack 						= MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution 						= OrderFillResolution.Standard;
                Slippage 									= 0;
                StartBehavior 								= StartBehavior.WaitUntilFlat;
                TimeInForce 								= TimeInForce.Gtc;
                TraceOrders 								= false;
                RealtimeErrorHandling 						= RealtimeErrorHandling.StopCancelCloseIgnoreRejects;
                StopTargetHandling 							= StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade 						= 50;
				RealtimeErrorHandling 						= RealtimeErrorHandling.StopCancelClose; // important to manage errors on rogue orders
                IsInstantiatedOnEachOptimizationIteration 	= false;
				
                // Default Parameters
				isAutoEnabled 					= true;
				isManualEnabled					= false;
				isLongEnabled					= true;
				isShortEnabled					= true;
				canTradeOK 						= true;
				
				OrderType						= OrderType.Limit;
				
		        // Choppiness Defaults
		        SlopeLookBack            		= 4;
		        FlatSlopeFactor       			= 0.125; 
		        ChopAdxThreshold        		= 25;
				EnableChoppinessDetection 		= true;
		        marketIsChoppy          		= false;
		        autoDisabledByChop      		= false;
				enableBackgroundSignal			= true;
				Opacity							= 50;       // Byte: 255 opaque
				
				enableBuySellPressure 			= true;
				showBuySellPressure 			= false;
				
				HmaPeriod 						= 16;
				enableHmaHooks 					= true;
				showHmaHooks 					= true;
	
				RegChanPeriod 					= 40;
				RegChanWidth 					= 4;
				RegChanWidth2 					= 3;
				enableRegChan1 					= true;
				enableRegChan2 					= true;
				showRegChan1 					= true;
				showRegChan2 					= true;
				showRegChanHiLo 				= true;

				enableVMA						= true;
				showVMA							= true;
				
				MomoUp							= 1;
				MomoDown						= -1;
				enableMomo						= true;
				showMomo						= false;
				
				adxPeriod						= 7;
				AdxThreshold					= 25;
				adxThreshold2					= 50;
				adxExitThreshold				= 45;
				enableADX						= true;
				showAdx							= false;
				
				emaLength						= 110;
				enableEMAFilter 				= false;
				showEMA							= false;
				
				AtrPeriod						= 14;
				atrThreshold					= 1.5;
				enableVolatility				= true;
				
				showPivots						= false;
				
				enableExit						= false;
				
				LimitOffset						= 4;
				TickMove						= 4;								
						
                MinRegChanTargetDistanceTicks = 60; // Example: Require at least 40 ticks for target
                MinRegChanStopDistanceTicks   = 120; // Example: Require at least 80 ticks distance for stop
				
				EnableFixedProfitTarget			= true; // Default
                EnableRegChanProfitTarget       = false; 
				EnableDynamicProfitTarget		= false;

				Contracts						= 1;
				Contracts2 						= 1;
				Contracts3 					    = 1;
				Contracts4						= 1;
				
				InitialStop						= 97;
				
				ProfitTarget					= 40;
				ProfitTarget2					= 44;
				ProfitTarget3					= 48;
				ProfitTarget4					= 52;
				
				EnableProfitTarget2				= true;
				EnableProfitTarget3				= true;
				EnableProfitTarget4				= true;
				
			//	Set BE Stop
				BESetAuto						= true;
				beSetAuto						= true;
				showctrlBESetAuto				= true;
				BE_Trigger						= 32;
				BE_Offset						= 4;
				_beRealized						= false;

			//	Trailing Stops
				enableTrail 					= true;
				tickTrail						= true;
				showTrailOptions 				= true;	
				trailStopType 					= TrailStopTypeKC.Tick_Trail;
				
			//	ATR Trail
				atrTrailSetAuto					= false;
				showAtrTrailSetAuto				= false;
				showAtrTrailOptions 			= false;
				enableAtrProfitTarget			= false;
				atrMultiplier					= 1.5;
				RiskRewardRatio					= 0.75;
//				Trail_frequency					= 4;
				
			//	3 Step Trail	
				showThreeStepTrailOptions 		= false;
				threeStepTrail					= false;
				step1ProfitTrigger 				= 1;	// Set your step 1 profit trigger
                step1StopLoss 					= 97;	// Set your step 1 stop loss
                step2ProfitTrigger 				= 44;	// Set your step 2 profit trigger
                step2StopLoss 					= 40;	// Set your step 2 stop loss
				step3ProfitTrigger 				= 52;	// Set your step 3 profit trigger
				step3StopLoss 					= 16;	// Set your step 3 stop loss
//				step1Frequency					= 4;
//				step2Frequency					= 4;
//				step3Frequency					= 2;
				ProgressState 					= 0;
				
				tradesPerDirection				= false;
				longPerDirection				= 5;
				shortPerDirection				= 5;	
				iBarsSinceExit					= 0;				
				SecsSinceEntry					= 0;
				
				QuickLong						= false;
				QuickShort						= false;
				
				counterLong						= 0;
				counterShort					= 0;
				
				Start							= DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
				End								= DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
				Start2							= DateTime.Parse("11:30", System.Globalization.CultureInfo.InvariantCulture);
				End2							= DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
				Start3							= DateTime.Parse("15:00", System.Globalization.CultureInfo.InvariantCulture);
				End3							= DateTime.Parse("18:00", System.Globalization.CultureInfo.InvariantCulture);
				Start4							= DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
				End4							= DateTime.Parse("03:30", System.Globalization.CultureInfo.InvariantCulture);
				Start5							= DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
				End5							= DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
				Start6							= DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
				End6							= DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);
				
				// Panel Status
				showDailyPnl					= true;
				PositionDailyPNL				= TextPosition.BottomLeft;	
				colorDailyProfitLoss			= Brushes.Cyan; // Default value
				
				showPnl							= false;
				PositionPnl						= TextPosition.TopLeft;
				colorPnl 						= Brushes.Yellow; // Default value
			
				// PnL Daily Limits
				dailyLossProfit					= true;
				DailyProfitLimit				= 100000;
				DailyLossLimit					= 1000;				
				TrailingDrawdown				= 1000;
				StartTrailingDD					= 3000;
				maxProfit 						= double.MinValue;	// double.MinValue guarantees that any totalPnL will trigger it to set the variable
				enableTrailingDrawdown 				= true;
				
				ShowHistorical					= true;
				
				useWebHook						= false;
				DiscordWebhooks					= "https://discord.com/channels/963493404988289124/1343311936736989194";
				
            }
            else if (State == State.Configure)
            {
				// Ensure RealtimeErrorHandling is set
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
				
				clientWebSocket = new ClientWebSocket();
				
				buyPressure = new Series<double>(this);
				sellPressure = new Series<double>(this);
            }
            else if (State == State.DataLoaded)
            {	
				hullMAHooks = BlueZHMAHooks(Close, HmaPeriod, 0, false, false, true, Brushes.Lime, Brushes.Red);
				hullMAHooks.Plots[0].Brush = Brushes.White;
				hullMAHooks.Plots[0].Width = 2;
				if (showHmaHooks) AddChartIndicator(hullMAHooks);
	
				RegressionChannel1 = RegressionChannel(Close, RegChanPeriod, RegChanWidth);
				if (showRegChan1) AddChartIndicator(RegressionChannel1);
	
				RegressionChannel2 = RegressionChannel(Close, RegChanPeriod, RegChanWidth2);
				if (showRegChan2) AddChartIndicator(RegressionChannel2);
	
				RegressionChannelHighLow1 = RegressionChannelHighLow(Close, RegChanPeriod, RegChanWidth);
				if (showRegChanHiLo) AddChartIndicator(RegressionChannelHighLow1);
	
				BuySellPressure1				= BuySellPressure(Close);
				BuySellPressure1.Plots[0].Width = 2;
				BuySellPressure1.Plots[0].Brush = Brushes.Lime;
				BuySellPressure1.Plots[1].Width = 2;
				BuySellPressure1.Plots[1].Brush = Brushes.Red;
				if (showBuySellPressure) AddChartIndicator(BuySellPressure1);
			
				VMA1				= VMA(Close, 9, 9);
				VMA1.Plots[0].Brush = Brushes.SkyBlue;
				VMA1.Plots[0].Width = 3;
				if (showVMA) AddChartIndicator(VMA1);			
				
				ATR1 	= ATR(AtrPeriod);
				        
				Momentum1			= Momentum(Close, 14);	
				Momentum1.Plots[0].Brush = Brushes.Yellow;
				Momentum1.Plots[0].Width = 2;
				if (showMomo) AddChartIndicator(Momentum1);
				
				ADX1				= ADX(Close, adxPeriod);
				ADX1.Plots[0].Brush = Brushes.Yellow;
				ADX1.Plots[0].Width = 2;
				if (showAdx) AddChartIndicator(ADX1);
				
				pivots = NTSvePivots(Close, false, NTSvePivotRange.Daily, NTSveHLCCalculationMode.CalcFromIntradayData, 0, 0, 0, 250);
				pivots.Plots[0].Width = 4;
				if (showPivots) AddChartIndicator(pivots);
				
				if(showEMA) 
				{
					AddChartIndicator(EMA(High, emaLength));
					AddChartIndicator(EMA(Low, emaLength));
				}
					
				if (additionalContractExists)
			    {
			        string quickProfitTargetLabel = isLong ? QLE : QSE;  // QLE = Quick Long Entry, QSE = Quick Short Entry
			        SetProfitTarget(quickProfitTargetLabel, CalculationMode.Ticks, ProfitTarget);
			    } 				
				
				// Initialize maxProfit with the totalPnL (if it's the first time, it's 0)
				maxProfit = totalPnL;  // Ensure maxProfit starts at the current PnL
            }
			else if (State == State.Historical)
			{
				// Chart Trader Buttons Load	
				Dispatcher.InvokeAsync((() => {	CreateWPFControls();	}));				
			}
			else if (State == State.Terminated)
			{
				// Chart Trader Buttons dispose
				ChartControl?.Dispatcher.InvokeAsync(() =>	{	DisposeWPFControls();	});
				
				clientWebSocket?.Dispose();	
				
				// Log any remaining active orders
				lock (orderLock)
				{
					if (activeOrders.Count > 0)
					{
						Print (string.Format("{0}: Strategy terminated with active orders. Investigate:", Time[0]));
						foreach (var kvp in activeOrders)
						{
							Print (string.Format("{0}: Order Label: {1}, Order ID: {2}", Time[0], kvp.Key, kvp.Value.OrderId));
							// Consider attempting to cancel the order.  Do this ONLY if you have
							// carefully considered the implications (e.g., potential for slippage)
							CancelOrder(kvp.Value); // IMPORTANT: Cancel rogue orders before terminating.
						}
					}
				}				
			}
        }
		#endregion	
		
        #region OnBarUpdate
		protected override void OnBarUpdate()
        {
			if(Time[0] - lastEntryTime < tradeDelay) return;

			canTradeOK = true; // Reset canTradeOK at the beginning of each bar.
			// Ensure enough bars have passed overall for the *longest period* required by indicators in this block.
			
            if (BarsInProgress != 0 || CurrentBars[0] < BarsRequiredToTrade || orderErrorOccurred)
				return;
			
			//Track the Highest Profit Achieved
			if (totalPnL > maxProfit)
			{
				maxProfit = totalPnL;
			}
			
			// *** Account Reconciliation ***
			if (DateTime.Now - lastAccountReconciliationTime > accountReconciliationInterval)
			{
				ReconcileAccountOrders();
				lastAccountReconciliationTime = DateTime.Now;
			}

			if (!ShowHistorical && State != State.Realtime) return;				
			
			trailStop = InitialStop;
			
			// Get pivot points and support/resistance levels
            pivotPoint = pivots.Pp[0];
            s1 = pivots.S1[0];
            s2 = pivots.S2[0];
            s3 = pivots.S3[0];
            r1 = pivots.R1[0];
            r2 = pivots.R2[0];
            r3 = pivots.R3[0];
            s1m = pivots.S1M[0];
            s2m = pivots.S2M[0];
			s3m = pivots.S3M[0];
            r1m = pivots.R1M[0];
            r2m = pivots.R2M[0];
			r3m = pivots.R3M[0];
			
			currentAtr = ATR1[0];
			atrUp = !enableVolatility || currentAtr > atrThreshold;
			
			currentAdx = ADX1[0];
			adxUp = !enableADX || currentAdx > AdxThreshold;
			
			currentMomentum = Momentum1[0];
			momoUp = !enableMomo || (currentMomentum > 0);
			momoDown = !enableMomo || (currentMomentum < 0);
			
			regChanUp = RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1];
			regChanDown = RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1];

			buyPressureUp = !enableBuySellPressure || (BuySellPressure1.BuyPressure[0] > BuySellPressure1.SellPressure[0]);
			sellPressureUp = !enableBuySellPressure || (BuySellPressure1.SellPressure[0] > BuySellPressure1.BuyPressure[0]);
			
			buyPressure[0] = BuySellPressure1.BuyPressure[0];
			sellPressure[0] = BuySellPressure1.SellPressure[0];
			
			hmaUp = (hullMAHooks[0] > hullMAHooks[1]);
			hmaDown = (hullMAHooks[0] < hullMAHooks[1]);
			
            // ***** START MODIFIED SECTION for VMA *****
            // Default values in case VMA isn't ready
            volMaUp = false;
            volMaDown = false;

            // Check if VMA1 is initialized and has calculated enough data for index 1
            if (VMA1 != null && VMA1.IsValidDataPoint(1)) // IsValidDataPoint checks index validity
            {
                // Safe to access VMA1[0] and VMA1[1]
                volMaUp = !enableVMA || VMA1[0] > VMA1[1];
                volMaDown = !enableVMA || VMA1[0] < VMA1[1];
            }
            // else: Keep the default false values calculated above if VMA isn't ready.
            // You could add a PrintOnce warning here if needed for debugging early bars.
            // ***** END MODIFIED SECTION for VMA *****

			aboveEMAHigh = !enableEMAFilter || Open[1] > EMA(High, emaLength)[1];
			belowEMALow = !enableEMAFilter || Open[1] < EMA(Low, emaLength)[1];
			
			if (EnableChoppinessDetection)
			{
				// --- Regression Channel Slope Choppiness ---
				marketIsChoppy = false; // Default
				
				// Check if enough bars exist for slope calculation AND for ADX
				if (CurrentBar >= Math.Max(RegChanPeriod, Math.Max(adxPeriod, SlopeLookBack)) -1 )
				{
				    double middleNow = RegressionChannel1.Middle[0];
				    double middleBefore = RegressionChannel1.Middle[SlopeLookBack];
				
				    // Calculate slope (change in price per bar)
				    double regChanSlope = (middleNow - middleBefore) / SlopeLookBack;
				
				    // Define a threshold for "flat" slope (needs tuning - VERY instrument dependent)
				    // Might be a fraction of TickSize, e.g., 0.1 * TickSize
				    double flatSlopeThreshold = FlatSlopeFactor * TickSize; // EXAMPLE - TUNE THIS CAREFULLY!
				
				    bool isRegChanFlat = Math.Abs(regChanSlope) < flatSlopeThreshold;
				    bool adxIsLow = currentAdx < ChopAdxThreshold; // Use existing ADX check
				
				    marketIsChoppy = isRegChanFlat && adxIsLow;
				}
				// --- End Regression Channel Slope Choppiness ---

			    // --- Manage Auto Trading Based on Choppiness ---
			    bool autoStatusChanged = false; // Flag to see if we changed status this bar				
				
				// Inside OnBarUpdate, after marketIsChoppy is calculated:
				if (marketIsChoppy) // Or your specific condition
				{
					TransparentColor(Opacity, Colors.LightGray);					
				}
				else
				{
				    // Reset the background when the condition is false
				    BackBrush = null; // Or Brushes.Transparent, or your default chart background if known
				}
					
			    if (marketIsChoppy)
			    {
			        if (isAutoEnabled) // Only act if it was enabled
			        {
			            isAutoEnabled = false;
			            autoDisabledByChop = true; // Mark that the *system* disabled it
			            autoStatusChanged = true;
			            Print($"{Time[0]}: Market choppy (ADX={currentAdx:F1} < {ChopAdxThreshold}, BBWidth Factor < {FlatSlopeFactor:P0}). Auto trading DISABLED.");
			        }
			    }
			    else // Market is NOT choppy
			    {
			        if (autoDisabledByChop) // Only re-enable if *system* disabled it
			        {
			            isAutoEnabled = true;
			            autoDisabledByChop = false; // Clear the flag
			            autoStatusChanged = true;
			            Print($"{Time[0]}: Market no longer choppy. Auto trading RE-ENABLED.");
			        }
			        // If autoDisabledByChop is false, it means the user turned it off manually, so we leave it off.
			    }

			    // --- Update Auto Button Visual ---
			    // Use Dispatcher for safety, although often works without in strategies
			    if (autoStatusChanged && autoBtn != null && ChartControl != null) // Check if button and chart control exist
			    {
			         ChartControl.Dispatcher.InvokeAsync(() => { // Ensures UI update happens on UI thread
			            DecorateButton(autoBtn, isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off");
			            // Also update manual button state inversely
			            DecorateButton(manualBtn, !isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off");
			         });
			    }
			}
			
			uptrend = adxUp && momoUp && buyPressureUp && hmaUp && volMaUp && regChanUp && atrUp && aboveEMAHigh;
            downtrend = adxUp && momoDown && sellPressureUp && hmaDown && volMaDown && regChanDown && atrUp && belowEMALow;
			
			if (RegressionChannel1.Middle[0] > RegressionChannel1.Middle[20])
			{
				PositionDailyPNL = TextPosition.TopLeft;
				PositionPnl	= TextPosition.BottomLeft;
			} 
			else
			{
				PositionDailyPNL = TextPosition.BottomLeft;
				PositionPnl	= TextPosition.TopLeft;
			}
				
			priceUp =  Close[0] > Close[1] && Close[0] > Open[0];		
			priceDown =  Close[0] < Close[1] && Close[0] < Open[0];
			
			UpdatePositionState();
			
			if (isAutoEnabled)
			{
				ProcessLongEntry();
				ProcessShortEntry();
			}
			
			if (enableBackgroundSignal)
			{
				if (uptrend)
				{				  
					TransparentColor(Opacity, Colors.Lime);
				}
				else if (downtrend)
				{			
					TransparentColor(Opacity, Colors.Crimson);
				}
				else
				{
				    // Reset the background when the condition is false
				    BackBrush = null; // Or Brushes.Transparent, or your default chart background if known
				}
			}	
				
		    // --- Stop/Target Management ---
		    ManageAutoBreakeven();
		    ManageStopLoss();
		    SetProfitTargets(); // Keep setting targets even if auto entry is off

			// Calculate Profit Target based on ATR
		    if (enableAtrProfitTarget) ProfitTarget = ATR1[0] * RiskRewardRatio / TickSize;
			
			if (EnableDynamicProfitTarget)
			{
				// Set profit target at each pivot level
				if (isLong && Close[0] > r3 && Low[0] <= r3)
					SetProfitTarget(@LE, CalculationMode.Ticks, ProfitTarget);
				else
					SetProfitTargetBasedOnLongConditions();
				
				if (isShort && Close[0] < s3 && High[0] >= s3)
					SetProfitTarget(@SE, CalculationMode.Ticks, ProfitTarget);
				else 
					SetProfitTargetBasedOnShortConditions();
			}
			
		    // --- PnL / Status Display ---
		    if (Bars.IsFirstBarOfSession)
			{
				cumPnL 			= totalPnL; ///Double that copies the full session PnL (If trading multiple days). Is only calculated once per day.
				dailyPnL		= totalPnL - cumPnL; ///Subtract the copy of the full session by the full session PnL. This resets your daily PnL back to 0.
			}
			
		    if (showPnl) ShowPNLStatus();

			if (showDailyPnl) DrawStrategyPnL();
				
//			if (IsFirstTickOfBar) checkPositions();    // Detect unwanted Positions opened (possible rogue Order?
			
			#region Reset Trades Per Direction
            if (TradesPerDirection){
                if (counterLong != 0 && Close[1] < Open[1])
                    counterLong = 0;
                if (counterShort != 0 && Close[1] > Open[1])
                    counterShort = 0;
            }
            #endregion	
			
			#region Reset Stop Loss
			
			// Reset when Flat
			if (isFlat)
			{
				// ***** NEW: Explicitly cancel working stops *****
                lock(orderLock) // Use existing lock for safety
                {
                    // Find all working stop market orders associated with ANY entry signal managed by this strategy
                    // You might need to refine the filter if you have stops from other sources/logics
                    List<Order> stopsToCancel = Orders.Where(o =>
                        o.OrderState == OrderState.Working &&
                        o.IsStopMarket &&
                        (o.FromEntrySignal == LE || o.FromEntrySignal == SE || o.FromEntrySignal == QLE || o.FromEntrySignal == QSE ||
                         o.FromEntrySignal == LE2 || o.FromEntrySignal == SE2 || o.FromEntrySignal == "QLE2" || o.FromEntrySignal == "QSE2" ||
                         o.FromEntrySignal == LE3 || o.FromEntrySignal == SE3 || o.FromEntrySignal == "QLE3" || o.FromEntrySignal == "QSE3" ||
                         o.FromEntrySignal == LE4 || o.FromEntrySignal == SE4 || o.FromEntrySignal == "QLE4" || o.FromEntrySignal == "QSE4" ||
                         o.FromEntrySignal == Add1LE || o.FromEntrySignal == Add1SE // Add any other relevant labels
                        )).ToList();

                    if (stopsToCancel.Count > 0)
                    {
                        PrintOnce($"Flat_CancelStops_{CurrentBar}", $"{Time[0]}: Position is flat. Cancelling {stopsToCancel.Count} working stop order(s).");
                        foreach (Order stopOrder in stopsToCancel)
                        {
                            try
                            {
                                 CancelOrder(stopOrder);
                                 // Optional: Remove from activeOrders if you were tracking stops there, unlikely here
                                 // if (activeOrders.ContainsValue(stopOrder)) { /* remove */ }
                            }
                            catch(Exception ex)
                            {
                                PrintOnce($"Flat_CancelStopError_{stopOrder.OrderId}_{CurrentBar}", $"{Time[0]}: Error cancelling stop order {stopOrder.OrderId} on flatten: {ex.Message}");
                                // Potentially set orderErrorOccurred = true; if stop cancel failure is critical
                            }
                        }
                    }
                }
				
				// Reset quick order buttons
			    quickLongBtnActive = false;
			    quickShortBtnActive = false;
			
			    // Reset counters and progress state
			    ProgressState = 0;
				trailStop = InitialStop;
		        lastStopLevel = InitialStop;
			    _beRealized = false;
				
				// Clear active orders on flatten. CRITICAL for ghost order prevention.
				lock (orderLock)
				{
					activeOrders.Clear();
				}
			}
			
			if (!canTradeOK)
			{
                if (Time[0] >= lastEntryTime.AddSeconds(SecsSinceEntry))
                {
					Print(Time[0] + " Timer de-activated");
                    canTradeOK = true;
                }				
			}	
			
			#endregion
			
			if (ValidateExitLong()) 
			{
				// Create the order labels array based on whether additional contracts exist
				string[] orderLabels = additionalContractExists ? new[] { LE2, LE3, LE4, QLE, "QLE2", "QLE3", "QLE4" } : new[] { LE };
				
				// Apply the initial stop for all relevant orders
				foreach (string label in orderLabels)
				{		              
					ExitLong(label);
				}
			}
			
			if (ValidateExitShort())
			{
				// Create the order labels array based on whether additional contracts exist
				string[] orderLabels = additionalContractExists ? new[] { SE2, SE3, SE4, QSE, "QSE2", "QSE3", "QSE4" } : new[] { SE };
				
				// Apply the initial stop for all relevant orders
				foreach (string label in orderLabels)
				{		              
					ExitShort(label);
				}	
			}
			
			KillSwitch();
        }
		#endregion
		
		#region Transparent Background Color
		private void TransparentColor(byte percentTransparency, Color baseColor)
		{
		    // percentTransparency = transparency, 50% = 128
		    // Create the new semi-transparent color
		    Color semiTransparentColor = Color.FromArgb(percentTransparency, baseColor.R, baseColor.G, baseColor.B);
		    // Create the new brush
		    SolidColorBrush semiTransparentBrush = new SolidColorBrush(semiTransparentColor);
		    // Freeze the brush for performance (important!)
		    semiTransparentBrush.Freeze();
		    // Assign the semi-transparent brush to BackBrush
		    BackBrush = semiTransparentBrush;
		}
		#endregion
			
		#region Breakeven Management

		// Helper method to determine the active order labels based on position
		private List<string> GetRelevantOrderLabels()
		{
		    List<string> labels = new List<string>();
		    bool isLongPosition = Position.MarketPosition == MarketPosition.Long;
		
		    // Add base labels depending on position type (Auto or Quick)
		    if (isLongPosition)
		    {
		        labels.Add(quickLongBtnActive ? QLE : LE); // QLE or LE
		        if (additionalContractExists) // Add scaled-in entries only if they exist conceptually
		        {
		             if (EnableProfitTarget2) labels.Add(quickLongBtnActive ? "QLE2" : LE2);
		             if (EnableProfitTarget3) labels.Add(quickLongBtnActive ? "QLE3" : LE3);
		             if (EnableProfitTarget4) labels.Add(quickLongBtnActive ? "QLE4" : LE4);
		        }
		    }
		    else // Short Position
		    {
		        labels.Add(quickShortBtnActive ? QSE : SE); // QSE or SE
		         if (additionalContractExists) // Add scaled entries only if they exist conceptually
		        {
		            if (EnableProfitTarget2) labels.Add(quickShortBtnActive ? "QSE2" : SE2);
		            if (EnableProfitTarget3) labels.Add(quickShortBtnActive ? "QSE3" : SE3);
		            if (EnableProfitTarget4) labels.Add(quickShortBtnActive ? "QSE4" : SE4);
		        }
		    }
		    // Add manual add-in labels if relevant (adjust based on your actual label usage)
		    // if (/* condition indicating Add1LE might be active */) labels.Add(Add1LE);
		    // if (/* condition indicating Add1SE might be active */) labels.Add(Add1SE);
		    return labels;
		}
		
		// Helper to safely set TRAILING stop loss (incorporates error handling)
		private void SetTrailingStop(string fromEntrySignal, CalculationMode mode, double value, bool isSimulatedStop = true)
		{
		     lock(orderLock) // Ensure thread safety
		     {
		         // Optional: Check if order already exists and is in a terminal state before modifying
		         // Relying on SetTrailStop's internal handling but wrap in try-catch.
		
		         try
		         {
		             // Use isSimulatedStop = true to keep strategy in control of trailing logic
		             SetTrailStop(fromEntrySignal, mode, value, isSimulatedStop);
		             Print($"{Time[0]}: SetTrailStop called for label '{fromEntrySignal}'. Mode: {mode}, Value: {value}, IsSimulated: {isSimulatedStop}");
		         }
		         catch (Exception ex)
		         {
		             Print($"{Time[0]}: Error calling SetTrailStop for label '{fromEntrySignal}': {ex.Message}");
		             orderErrorOccurred = true; // Flag the error
		         }
		     }
		}

		// Main method to manage the automatic breakeven logic for EITHER Fixed or Trailing Stops
        private void ManageAutoBreakeven()
        {
            // --- Pre-checks ---
            if (isFlat || !beSetAuto || _beRealized) return;

            // --- Check for Override Condition ---
            bool useRegChanOverride = (TrailStopType == TrailStopTypeKC.Regression_Channel_Trail && EnableRegChanProfitTarget);
            int effectiveBeTrigger = useRegChanOverride ? 60 : BE_Trigger; // Use 60 if override active
            int effectiveBeOffset = useRegChanOverride ? 4 : BE_Offset;   // Use 4 if override active

            if (useRegChanOverride)
                 PrintOnce($"BE_Override_{CurrentBar}", $"{Time[0]}: RegChan Trail & Target active. Using OVERRIDE BE Trigger = {effectiveBeTrigger}, BE Offset = {effectiveBeOffset}.");


            // --- Calculation & Logging ---
            double currentUnrealizedPnlTicks = Position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks, Close[0]);
            // Reduced frequent logging unless debugging BE itself
            // Print($"{Time[0]}: Checking Auto BE. PnL Ticks: {currentUnrealizedPnlTicks:F2}, Trigger: {effectiveBeTrigger}, Offset: {effectiveBeOffset}, Realized: {_beRealized}");


            // --- Trigger Condition (using effective trigger) ---
            if (currentUnrealizedPnlTicks >= effectiveBeTrigger)
            {
                PrintOnce($"BE_Triggered_{CurrentBar}", $"{Time[0]}: Auto-Breakeven triggered. PnL (Ticks): {currentUnrealizedPnlTicks:F2} >= Trigger: {effectiveBeTrigger}");

                // --- Calculate Target Breakeven Stop Price (using effective offset) ---
                double entryPrice = Position.AveragePrice;
                if (entryPrice == 0) { PrintOnce($"BE_EntryZero_{CurrentBar}",$"{Time[0]}: ManageAutoBreakeven - Cannot calculate, entry price is 0."); return; }

                double offsetPriceAdjustment = effectiveBeOffset * TickSize; // Use effective offset
                double breakevenStopPrice = entryPrice + (Position.MarketPosition == MarketPosition.Long ? offsetPriceAdjustment : -offsetPriceAdjustment);

                PrintOnce($"BE_Calc_{CurrentBar}", $"{Time[0]}: Calculated Breakeven Stop Price: {breakevenStopPrice:F5} (Entry: {entryPrice:F5}, Offset Ticks Used: {effectiveBeOffset})");

                // --- Apply Stop Based on Strategy Setting ---
                List<string> relevantLabels = GetRelevantOrderLabels();
                if (relevantLabels.Count == 0) { PrintOnce($"BE_NoLabels_{CurrentBar}",$"{Time[0]}: Warning: Breakeven triggered but no relevant order labels found."); return; }

                bool stopAppliedSuccessfully = false;

                // --- Decide if Stop is Fixed or Trailing ---
                // NOTE: Breakeven logic MODIFIES the existing stop. It needs to know if the active stop is fixed or trailing.
                //       The 'enableTrail' and 'enableFixedStopLoss' flags reflect the strategy setting, NOT necessarily the current stop type on the chart
                //       if settings were changed mid-trade. It's safer to assume the mode based on the strategy setting *at the time BE triggers*.
 
                if (enableTrail) // If the strategy is SET to use trailing stops
                {
                    double currentMarketPrice = Close[0];
                    double valueInTicks;
                    if (Position.MarketPosition == MarketPosition.Long) valueInTicks = (currentMarketPrice - breakevenStopPrice) / TickSize;
                    else valueInTicks = (breakevenStopPrice - currentMarketPrice) / TickSize;

                    PrintOnce($"BE_OtherTrail_CalcTicks_{CurrentBar}", $"{Time[0]}: Calculated Trailing Value (Ticks) for SetTrailStop (Non-RegChan BE): {valueInTicks:F2}");

                    if (valueInTicks <= 0 || !IsValidStopPlacement(breakevenStopPrice, Position.MarketPosition)) // Add validation
                    {
                         PrintOnce($"BE_OtherTrail_Skip_{CurrentBar}",$"{Time[0]}: Warning: Cannot apply TRAILING BE stop. Price {breakevenStopPrice:F5} / Ticks {valueInTicks:F2} invalid relative to market {currentMarketPrice:F5}.");
                    }
                    else
                    {
                        PrintOnce($"BE_OtherTrail_Apply_{CurrentBar}",$"{Time[0]}: Applying TRAILING Breakeven Stop (Ticks from Market: {valueInTicks:F2}) to labels: {string.Join(", ", relevantLabels)}");
                        foreach (string tag in relevantLabels)
                        {
                            // Use SetTrailStop to manage the trailing stop from the new BE point
                            SetTrailingStop(tag, CalculationMode.Ticks, valueInTicks, true);
                        }
                        stopAppliedSuccessfully = true;
                    }
                }
                else if (enableFixedStopLoss) // If the strategy is SET to use fixed stops
                {
                    // Move the existing FIXED stop (placed by Exit...StopMarket) to the breakeven PRICE
                    if (!IsValidStopPlacement(breakevenStopPrice, Position.MarketPosition)) // Validate placement
                    {
                        PrintOnce($"BE_FixedStop_Invalid_{CurrentBar}", $"{Time[0]}: Warning: Cannot apply FIXED BE stop. Price {breakevenStopPrice:F5} invalid relative to market.");
                    }
                    else
                    {
                        PrintOnce($"BE_FixedStop_Apply_{CurrentBar}", $"{Time[0]}: Applying FIXED Breakeven Stop (Price: {breakevenStopPrice:F5}) to labels: {string.Join(", ", relevantLabels)}");
                        foreach (string tag in relevantLabels)
                        {
                            // Use SetFixedStopLoss helper (which uses Exit...StopMarket to cancel/replace)
                            SetFixedStopLoss(tag, CalculationMode.Price, breakevenStopPrice, false);
                        }
                        stopAppliedSuccessfully = true;
                    }
                }
                else
                {
                     PrintOnce($"BE_NoMode_{CurrentBar}",$"{Time[0]}: Warning: Breakeven triggered but neither Fixed Stop nor Trailing Stop is enabled in strategy settings.");
                }

                // --- Mark as Realized ---
                if (stopAppliedSuccessfully)
                {
                    _beRealized = true;
                    PrintOnce($"BE_Realized_{CurrentBar}", $"{Time[0]}: Auto-Breakeven process complete for this bar. _beRealized set to true.");
                }
            }
        }
		#endregion
		
		#region Stop Loss Management

        // ***** MODIFIED SECTION *****
		// Helper to safely set stop loss (incorporates error handling)
        // THIS VERSION NOW USES ExitLongStopMarket / ExitShortStopMarket
		private void SetFixedStopLoss(string fromEntrySignal, CalculationMode mode, double priceValue, bool isSimulatedStop = false) // isSimulatedStop is now ignored
		{
		     lock(orderLock) // Ensure thread safety
		     {
		         if (Position.MarketPosition == MarketPosition.Flat)
                 {
                     PrintOnce($"SetFixedStop_Flat_{fromEntrySignal}_{CurrentBar}",$"{Time[0]}: Cannot set fixed stop for '{fromEntrySignal}'. Position is flat.");
                     return;
                 }

                 if (Position.Quantity == 0)
                 {
                      PrintOnce($"SetFixedStop_ZeroQty_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Cannot set fixed stop for '{fromEntrySignal}'. Position quantity is zero.");
                      return;
                 }

                 double stopPrice = 0;

                 // Calculate the target stop price based on mode
                 if (mode == CalculationMode.Price)
                 {
                     stopPrice = priceValue;
                 }
                 else if (mode == CalculationMode.Ticks)
                 {
                     double entryPrice = Position.AveragePrice;
                     if (entryPrice == 0) { PrintOnce($"SetFixedStop_EntryZero_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Cannot calculate stop price from Ticks for '{fromEntrySignal}'. Entry price is 0."); return; }
                     if (TickSize <= 0) { PrintOnce($"SetFixedStop_TickSize_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Cannot calculate stop price from Ticks for '{fromEntrySignal}'. Invalid TickSize."); return; }

                     stopPrice = (Position.MarketPosition == MarketPosition.Long)
                                 ? entryPrice - (priceValue * TickSize)
                                 : entryPrice + (priceValue * TickSize);
                 }
                 else
                 {
                     PrintOnce($"SetFixedStop_BadMode_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Cannot set fixed stop for '{fromEntrySignal}'. Invalid CalculationMode: {mode}.");
                     return;
                 }

                // --- Validation ---
                if (!IsValidStopPlacement(stopPrice, Position.MarketPosition))
                {
                     PrintOnce($"SetFixedStop_InvalidPlace_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Fixed stop placement {stopPrice:F5} is invalid for '{fromEntrySignal}'. Skipping submission.");
                     return; // Skip submitting invalid stop
                }

                // --- Submit Exit Order ---
                // NOTE: This exits the ENTIRE quantity associated with the fromEntrySignal.
                // If you need finer control per execution, more complex logic is needed.
                int quantityToExit = Position.Quantity; // Exit the full position quantity tied to this signal trigger. Be aware if scaling out.
                string ocoGroup = ""; // Typically empty unless managing complex OCO manually.
                string signalTag = "Fixed_Stop_" + fromEntrySignal; // Unique tag for this stop order

		         try
		         {
                     if (Position.MarketPosition == MarketPosition.Long)
                     {
                         ExitLongStopMarket(quantityToExit, stopPrice, signalTag, fromEntrySignal);
                         PrintOnce($"SetFixedStop_SubmitL_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Submitted ExitLongStopMarket ({quantityToExit} @ {stopPrice:F5}) for label '{fromEntrySignal}'. Tag: {signalTag}");
                     }
                     else if (Position.MarketPosition == MarketPosition.Short)
                     {
                         ExitShortStopMarket(quantityToExit, stopPrice, signalTag, fromEntrySignal);
                         PrintOnce($"SetFixedStop_SubmitS_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Submitted ExitShortStopMarket ({quantityToExit} @ {stopPrice:F5}) for label '{fromEntrySignal}'. Tag: {signalTag}");
                     }
		         }
		         catch (Exception ex)
		         {
		             PrintOnce($"SetFixedStop_Error_{fromEntrySignal}_{CurrentBar}", $"{Time[0]}: Error submitting Exit...StopMarket for label '{fromEntrySignal}': {ex.Message}");
		             orderErrorOccurred = true; // Flag the error
		         }
		     }
		}
        // ***** END OF MODIFIED SECTION *****

		#endregion
		
		#region Set Stop Losses

        // ***** MODIFIED SECTION *****
        // This method now ALWAYS calculates an initial stop PRICE.
        // If trailing, it uses Exit...StopMarket for initial placement.
//        // If fixed, it uses the SetFixedStopLoss helper (which also uses Exit...StopMarket).
//        private void SetStopLosses(string entryOrderLabel)
//        {
//            // --- Pre-checks ---
//            if (Position.MarketPosition == MarketPosition.Flat) return;
//            if (TickSize <= 0) { PrintOnce($"SetStopLosses_TickSize_{entryOrderLabel}_{CurrentBar}", $"{Time[0]}: Cannot set stops for {entryOrderLabel}. Invalid TickSize."); return; }

//            double initialStopPrice; // Always calculate price now

//            // --- Check for Override Condition ---
//            bool useRegChanOverride = (TrailStopType == TrailStopTypeKC.Regression_Channel_Trail && EnableRegChanProfitTarget);
//            int effectiveInitialStopTicks = useRegChanOverride ? 120 : InitialStop; // Use 120 if override is active

//            if (useRegChanOverride)
//                 PrintOnce($"SetStopLosses_OverrideStop_{entryOrderLabel}_{CurrentBar}", $"{Time[0]}: RegChan Trail & Target active. Using OVERRIDE InitialStop = {effectiveInitialStopTicks} ticks.");


//            // --- Determine Reference Price ---
//            double referencePrice = 0;
//            if (Position.MarketPosition == MarketPosition.Long) referencePrice = GetCurrentBid();
//            else if (Position.MarketPosition == MarketPosition.Short) referencePrice = GetCurrentAsk();

//            if (referencePrice == 0)
//            {
//                PrintOnce($"SetStopLosses_RefPrice_{entryOrderLabel}_{CurrentBar}", $"{Time[0]}: Cannot determine reference price for initial stop for {entryOrderLabel}. Skipping initial stop.");
//                return;
//            }

//            // --- Calculate Initial Stop Price using effective ticks ---
//            initialStopPrice = (Position.MarketPosition == MarketPosition.Long)
//                                 ? referencePrice - (effectiveInitialStopTicks * TickSize) // Use effective value
//                                 : referencePrice + (effectiveInitialStopTicks * TickSize); // Use effective value

//            PrintOnce($"SetStopLosses_InitPrice_{entryOrderLabel}_{CurrentBar}", $"{Time[0]}: [Initial Stop Setup] Calculated initial stop PRICE: {initialStopPrice:F5} for label {entryOrderLabel} (Ref: {referencePrice:F5}, Ticks Used: {effectiveInitialStopTicks}).");

//            // --- Validate the calculated stop price ---
//            if (!IsValidStopPlacement(initialStopPrice, Position.MarketPosition))
//            {
//                 PrintOnce($"SetStopLosses_InvalidPlace_{entryOrderLabel}_{CurrentBar}", $"{Time[0]}: Initial stop placement {initialStopPrice:F5} is invalid for '{entryOrderLabel}'. Skipping initial stop placement.");
//                 return;
//            }

//            // --- Apply Stop to Primary Label using Explicit Exit Orders ---
//            SetFixedStopLoss(entryOrderLabel, CalculationMode.Price, initialStopPrice, false);
//             string modeInfo = enableTrail ? "(Intended for Trail)" : "(Fixed)";
//             PrintOnce($"SetStopLosses_Apply_{entryOrderLabel}_{CurrentBar}", $"{Time[0]}: Applied initial stop using Exit...StopMarket(Price: {initialStopPrice:F5}) {modeInfo} for label {entryOrderLabel}.");

//            // --- Apply Stop to Scaled-In Labels ---
//            SetMultipleStopLosses(initialStopPrice, enableTrail);
//        }
		
        // (SetMultipleStopLosses does not need changes here as it receives the calculated price)
        // ... SetMultipleStopLosses remains the same ...
        private void SetMultipleStopLosses(double initialStopPrice, bool isTrailingIntendedLater) // Changed parameter name for clarity
		{
            string modeDesc = isTrailingIntendedLater ? "Initial Placement (for later Trail)" : "Initial Placement (Fixed)";
            PrintOnce($"SetMultiStopLosses_Start_{CurrentBar}", $"{Time[0]}: SetMultipleStopLosses called. Mode: {modeDesc}, Initial Price: {initialStopPrice:F5}");

			if (enableFixedProfitTarget) // Only applies if scale-ins are possible
            {
                // Determine label prefix (same logic as before)
                string labelPrefix = "";
                MarketPosition currentPositionState = Position.MarketPosition;
                if (currentPositionState == MarketPosition.Long) labelPrefix = quickLongBtnActive ? QLE : LE;
                else if (currentPositionState == MarketPosition.Short) labelPrefix = quickShortBtnActive ? QSE : SE;
                else { PrintOnce($"SetMultiStopLosses_Flat_{CurrentBar}", $"{Time[0]}: SetMultipleStopLosses - Cannot determine label prefix, position is flat."); return; }
                PrintOnce($"SetMultiStopLosses_Prefix_{labelPrefix}_{CurrentBar}", $"{Time[0]}: SetMultipleStopLosses - Prefix: {labelPrefix}");

                var scaleInTargets = new[] {
                    new { Enabled = EnableProfitTarget2, Suffix = "2"},
                    new { Enabled = EnableProfitTarget3, Suffix = "3"},
                    new { Enabled = EnableProfitTarget4, Suffix = "4"}
                };

                foreach (var target in scaleInTargets)
                {
                    if (target.Enabled)
                    {
                        string lbl = labelPrefix + target.Suffix;
                        PrintOnce($"SetMultiStopLosses_Apply_{lbl}_{CurrentBar}", $"{Time[0]}: SetMultipleStopLosses - Applying initial stop for label {lbl}.");
                        try
                        {
                            // ALWAYS use SetFixedStopLoss (Exit...StopMarket) for initial placement
                            SetFixedStopLoss(lbl, CalculationMode.Price, initialStopPrice, false);
                        }
                        catch(Exception ex)
                        {
                             PrintOnce($"SetMultiStopLosses_Error_{lbl}_{CurrentBar}",$"{Time[0]}: Error applying initial stop for label {lbl}: {ex.Message}");
                             orderErrorOccurred = true;
                        }
                    }
                }
			}
            else
            {
                 PrintOnce($"SetMultiStopLosses_SkipNoScale_{CurrentBar}", $"{Time[0]}: SetMultipleStopLosses - enableFixedProfitTarget is false, skipping scale-in stops.");
            }
		}

		#endregion

        // ***** MODIFIED SECTION *****
		#region Stop Loss Management

        // The SetFixedStopLoss helper (using Exit...StopMarket) remains unchanged from the previous version

		// Helper method to calculate the trailing stop value in ticks based on the active mode
        // (This function remains unchanged)
		private double CalculateTrailingStopTicks()
		{
            // ... (no changes here) ...
            double calculatedTrailStopTicks = InitialStop; // Default to InitialStop (acts like Tick Trail default)

		    if (tickTrail)
		    {
		        calculatedTrailStopTicks = trailStop;
		        // Print($"[DEBUG Tick Trail] Using InitialStop: {calculatedTrailStopTicks}"); // Reduced logging
		    }
		    else if (threeStepTrail)
		    {
		        // ... (3-step logic unchanged) ...
		        double currentUnrealizedPnlTicks = Position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks, Close[0]);
		        int currentProgressState = ProgressState; // Capture current state before potential change
		        switch (currentProgressState)
		        {
		            case 0:
		                calculatedTrailStopTicks = InitialStop; // Use InitialStop before first trigger
		                if (currentUnrealizedPnlTicks >= step1ProfitTrigger)
		                {
		                    ProgressState = 1;
		                    calculatedTrailStopTicks = step1StopLoss;
		                    PrintOnce($"TS_3Step_State1_{CurrentBar}", $"{Time[0]}: [3-Step Trail] State 1. PnL: {currentUnrealizedPnlTicks:F2} >= Trigger: {step1ProfitTrigger}. Stop Ticks: {calculatedTrailStopTicks}");
		                }
		                break;
		            case 1:
		                calculatedTrailStopTicks = step1StopLoss;
		                if (currentUnrealizedPnlTicks >= step2ProfitTrigger)
		                {
		                    ProgressState = 2;
		                    calculatedTrailStopTicks = step2StopLoss;
		                     PrintOnce($"TS_3Step_State2_{CurrentBar}", $"{Time[0]}: [3-Step Trail] State 2. PnL: {currentUnrealizedPnlTicks:F2} >= Trigger: {step2ProfitTrigger}. Stop Ticks: {calculatedTrailStopTicks}");
		                }
		                break;
		            case 2:
		                calculatedTrailStopTicks = step2StopLoss;
		                if (currentUnrealizedPnlTicks >= step3ProfitTrigger)
		                {
		                    ProgressState = 3; // Explicitly go to state 3
		                    calculatedTrailStopTicks = step3StopLoss;
		                    PrintOnce($"TS_3Step_State3_{CurrentBar}", $"{Time[0]}: [3-Step Trail] State 3 Trigger. PnL: {currentUnrealizedPnlTicks:F2} >= Trigger: {step3ProfitTrigger}. Stop Ticks: {calculatedTrailStopTicks}");
		                }
		                break;
                    case 3: // Keep using step 3 stop once reached
                        calculatedTrailStopTicks = step3StopLoss;
                        break;
		        }
		        // Print($"[DEBUG 3-Step] Current State: {currentProgressState}, Calculated Stop Ticks: {calculatedTrailStopTicks}"); // Reduced logging
		    }
		    else if (atrTrailSetAuto)
		    {
		        // ... (ATR logic unchanged) ...
		        if (ATR1 != null && ATR1.IsValidDataPoint(0) && TickSize > 0)
		        {
		            calculatedTrailStopTicks = Math.Max(1, ATR1[0] * atrMultiplier / TickSize); // Ensure at least 1 tick
		            // Print($"[DEBUG ATR Trail] ATR: {ATR1[0]:F5}, Calculated Stop Ticks: {calculatedTrailStopTicks:F2}"); // Reduced logging
		        }
		        else
		        {
		             // Print($"[WARN ATR Trail] ATR not ready or TickSize invalid. Using default: {calculatedTrailStopTicks}"); // Reduced logging
		        }
		    }

		    return calculatedTrailStopTicks;
		}

		// Main method to manage stop loss based on active settings
        // NOW, this method ACTIVATES and MANAGES trailing using SetTrailStop if enableTrail is true
		private void ManageStopLoss()
		{
		    if (isFlat) return;

		    List<string> relevantLabels = GetRelevantOrderLabels();
			if (relevantLabels.Count == 0) { PrintOnce($"ManageSL_NoLabels_{CurrentBar}","Warning: ManageStopLoss called but no relevant order labels found."); return; }

            // --- Check for Override Condition ---
            bool useRegChanOverride = (TrailStopType == TrailStopTypeKC.Regression_Channel_Trail && EnableRegChanProfitTarget);

            // --- If Trailing is Enabled: Activate and Manage with SetTrailStop ---
		    if (enableTrail)
		    {
                // Calculate the DESIRED trailing stop distance/price based on the ACTIVE trail type for THIS bar
                double trailValue = 0;
                CalculationMode trailMode = CalculationMode.Ticks; // Default, ATR and 3Step use Ticks

                if (trailStopType == TrailStopTypeKC.Regression_Channel_Trail)
                {
                    // --- Regression Channel Trailing ---
                    if (CurrentBar < RegChanPeriod - 1 || TickSize <= 0) return;

                    double targetStopPrice = 0;
                    string targetBandName = "";
                    bool useFallbackStop = false;
                    if (Position.MarketPosition == MarketPosition.Long) { targetStopPrice = RegressionChannel1.Lower[0]; targetBandName = "Lower"; }
                    else if (Position.MarketPosition == MarketPosition.Short) { targetStopPrice = RegressionChannel1.Upper[0]; targetBandName = "Upper"; }
                    else return;

                    double currentMarketPrice = Close[0];
                    double distanceTicks = Math.Abs(currentMarketPrice - targetStopPrice) / TickSize;

                    if (distanceTicks < MinRegChanStopDistanceTicks) useFallbackStop = true; // Fallback check

                    // ***** MODIFIED: Apply override to fallback value *****
                    int effectiveInitialStopTicks = useRegChanOverride ? 120 : InitialStop; // Use 120 if override active

                    if (useFallbackStop) {
                        trailValue = effectiveInitialStopTicks; // Use the effective fallback value
                        trailMode = CalculationMode.Ticks;
                        string fallbackReason = useRegChanOverride ? $"OVERRIDE ({effectiveInitialStopTicks} ticks)" : $"Parameter ({effectiveInitialStopTicks} ticks)";
                        PrintOnce($"ManageSL_RegChanFallback_{CurrentBar}", $"{Time[0]}: ManageSL RegChan Fallback to {fallbackReason}.");
                    }
                    // ***** END MODIFICATION *****
                    else {
                        // Calculate trail ticks based on the channel band price
                        if (Position.MarketPosition == MarketPosition.Long) trailValue = (currentMarketPrice - targetStopPrice) / TickSize;
                        else trailValue = (targetStopPrice - currentMarketPrice) / TickSize;
                        trailMode = CalculationMode.Ticks;
                        PrintOnce($"ManageSL_RegChanApply_{CurrentBar}", $"{Time[0]}: ManageSL RegChan Apply at {targetBandName} Band. Trail Ticks: {trailValue:F2}");
                    }
                     if (trailValue <= 0) { PrintOnce($"ManageSL_RegChanNegTicks_{CurrentBar}", $"{Time[0]}: ManageSL RegChan calculated non-positive ticks ({trailValue:F2}). Skipping."); return; }
                }
                else // Handle other trail types (Tick, ATR, 3-Step) which use Ticks
                {
                    trailValue = CalculateTrailingStopTicks(); // Get Ticks for Tick, ATR, 3-Step
                    trailMode = CalculationMode.Ticks;
                    if (trailValue <= 0) { PrintOnce($"ManageSL_OtherNegTicks_{trailStopType}_{CurrentBar}", $"{Time[0]}: ManageSL {trailStopType} calculated non-positive ticks ({trailValue:F2}). Skipping."); return; }
                     PrintOnce($"ManageSL_OtherApply_{trailStopType}_{CurrentBar}", $"{Time[0]}: ManageSL {trailStopType} Apply. Trail Ticks: {trailValue:F2}");
                }

                // --- Apply/Update Trailing Stop using SetTrailStop ---
                foreach (string label in relevantLabels)
                {
                    SetTrailingStop(label, trailMode, trailValue, true);
                }
		    }
		}

		#endregion
        // ***** END OF MODIFIED SECTION *****
		
		#region Helper Methods

        /// <summary>
        /// Validates if a target stop price is valid relative to the current market Bid/Ask.
        /// Includes a small buffer to account for transmission delays and slippage.
        /// </summary>
        /// <param name="targetStopPrice">The intended new stop price.</param>
        /// <param name="position">The current market position (Long or Short).</param>
        /// <param name="bufferTicks">Number of ticks buffer to apply. Adjust based on instrument volatility.</param>
        /// <returns>True if the price is valid, False otherwise.</returns>
        private bool IsValidStopPlacement(double targetStopPrice, MarketPosition position, int bufferTicks = 4) // Default buffer of 4 ticks (e.g., 1 point on MNQ/NQ)
        {
            // --- Essential Pre-Checks ---
            if (TickSize <= 0)
            {
                Print($"{Time[0]}: Validation FAIL: Invalid TickSize {TickSize}. Cannot validate stop.");
                return false;
            }
            // Ensure we have valid market data access (might not be strictly necessary in OnBarUpdate but good practice)
            if (!IsMarketDataValid())
            {
                 Print($"{Time[0]}: Validation FAIL: Market data (Bid/Ask) not available. Cannot validate stop.");
                 return false;
            }

            // --- Validation Logic ---
            if (position == MarketPosition.Long) // Validating a Sell Stop Order
            {
                double currentAsk = GetCurrentAsk();
                if (currentAsk == 0) { Print($"{Time[0]}: Validation WARN: Current Ask is 0. Cannot reliably validate Sell Stop."); return false; } // Cannot validate against 0

                // Sell Stop must be placed BELOW the current Ask price, including the buffer.
                double minStopLevel = currentAsk - bufferTicks * TickSize;
                if (targetStopPrice >= minStopLevel)
                {
                    Print($"{Time[0]}: Validation FAIL: Target Sell Stop {targetStopPrice:F5} is >= Ask {currentAsk:F5} (minus {bufferTicks} tick buffer {minStopLevel:F5}).");
                    return false;
                }
            }
            else if (position == MarketPosition.Short) // Validating a Buy Stop Order
            {
                double currentBid = GetCurrentBid();
                 if (currentBid == 0) { Print($"{Time[0]}: Validation WARN: Current Bid is 0. Cannot reliably validate Buy Stop."); return false; } // Cannot validate against 0

                // Buy Stop must be placed ABOVE the current Bid price, including the buffer.
                double maxStopLevel = currentBid + bufferTicks * TickSize;
                if (targetStopPrice <= maxStopLevel)
                {
                     Print($"{Time[0]}: Validation FAIL: Target Buy Stop {targetStopPrice:F5} is <= Bid {currentBid:F5} (plus {bufferTicks} tick buffer {maxStopLevel:F5}).");
                    return false;
                }
            }
            else // Position is Flat or Unknown
            {
                 Print($"{Time[0]}: Validation FAIL: Position is Flat or Unknown ({position}). Cannot validate stop.");
                return false; // Cannot validate if not in a position
            }

            // If all relevant checks passed
            Print($"{Time[0]}: Validation PASS: Target Stop {targetStopPrice:F5} is valid for position {position}.");
            return true;
        }

        /// <summary>
        /// Helper to check if essential market data (Bid/Ask) is available.
        /// </summary>
        /// <returns>True if Bid/Ask are likely available, False otherwise.</returns>
        private bool IsMarketDataValid()
        {
            // A simple check. More robust checks might involve looking at connection status or last update time if available.
            return GetCurrentBid() > 0 && GetCurrentAsk() > 0;
        }

		// --- Keep other existing helper methods like CanSubmitOrder ---

		#endregion // End Helper Methods

		#region Update Position State
		private void UpdatePositionState()
		{
			isLong = Position.MarketPosition == MarketPosition.Long;
			isShort = Position.MarketPosition == MarketPosition.Short;
			isFlat = Position.MarketPosition == MarketPosition.Flat;
			
			entryPrice = Position.AveragePrice;
			currentPrice = Close[0];
				
			// Logic to check if additional contracts exist (i.e., more than one contract is held)
		    additionalContractExists = Position.Quantity > 1;			
		}
		#endregion

		#region Long Entry			
		private void ProcessLongEntry()
		{
			if (IsLongEntryConditionMet())
		    {
				EnterLongPosition();
		    }
		}			
		#endregion
		
		#region Short Entry
		private void ProcessShortEntry()
		{
            if (IsShortEntryConditionMet())
		    {
				EnterShortPosition();
		    }	
		}			
		#endregion
			
		#region Entry Condition Checkers

        private bool IsLongEntryConditionMet()
        {
			// Combine all entry conditions into a single, readable expression
            return ValidateEntryLong()
                   && isLongEnabled
                   && checkTimers()
                   && (dailyLossProfit ? dailyPnL > -DailyLossLimit && dailyPnL < DailyProfitLimit : true)
                   && isFlat
                   && uptrend
                   && !trailingDrawdownReached
                   && (iBarsSinceExit > 0 ? BarsSinceExitExecution(0, "", 0) > iBarsSinceExit : BarsSinceExitExecution(0, "", 0) > 1 || BarsSinceExitExecution(0, "", 0) == -1)
                   && canTradeOK
                   && (!TradesPerDirection || (TradesPerDirection && counterLong < longPerDirection));
        }

        private bool IsShortEntryConditionMet()
        {
            return ValidateEntryShort()
				&& isShortEnabled
				&& checkTimers()
				&& (dailyLossProfit ? dailyPnL > -DailyLossLimit && dailyPnL < DailyProfitLimit : true)
				&& isFlat
				&& downtrend
				&& !trailingDrawdownReached
				&& (iBarsSinceExit > 0 ? BarsSinceExitExecution(0, "", 0) > iBarsSinceExit : BarsSinceExitExecution(0, "", 0) > 1 || BarsSinceExitExecution(0, "", 0) == -1)
				&& canTradeOK
				&& (!TradesPerDirection || (TradesPerDirection && counterShort < shortPerDirection));
        }

        #endregion

        #region Entry Execution

        // ***** MODIFIED SECTION ***** REMOVED SetStopLosses/SetProfitTargets calls
        private void EnterLongPosition()
        {
            counterLong += 1;
            counterShort = 0;
            string primaryLabel = LE;

            // --- 1. Submit Base Entry Order ---
            Order baseOrder = SubmitEntryOrder(primaryLabel, OrderType, Contracts);
            if (baseOrder == null)
            {
                Print($"{Time[0]}: Failed to submit base long entry order {primaryLabel}. Aborting entry sequence.");
                counterLong -= 1;
                return;
            }
            Draw.Dot(this, primaryLabel + Convert.ToString(CurrentBars[0]), false, 0, (Close[0]), Brushes.Cyan);
            lastEntryTime = Time[0];
            PrintOnce($"Entry_Base_Submit_{primaryLabel}_{CurrentBar}", $"{Time[0]}: Submitted base long entry: {primaryLabel}");

            // --- 2. Submit Scale-In Entry Orders ---
            EnterMultipleLongContracts(false);

            // --- 3. Set Initial Stop Losses (REMOVED FROM HERE) ---
            // SetStopLosses(primaryLabel); // <<< REMOVED

            // --- 4. Set Profit Targets (REMOVED FROM HERE) ---
            // SetProfitTargets(); // <<< REMOVED

            // --- 5. Send Discord Signal (Optional) ---
            // ... (Discord logic remains, but consider if it should also move to OnExecutionUpdate) ...
            // It might be better to send the signal AFTER confirmation of fill and stop/target placement.
        }

         // ***** MODIFIED SECTION ***** REMOVED SetStopLosses/SetProfitTargets calls
        private void EnterShortPosition()
        {
            counterLong = 0;
            counterShort += 1;
            string primaryLabel = SE;

            // --- 1. Submit Base Entry Order ---
            Order baseOrder = SubmitEntryOrder(primaryLabel, OrderType, Contracts);
             if (baseOrder == null)
            {
                Print($"{Time[0]}: Failed to submit base short entry order {primaryLabel}. Aborting entry sequence.");
                counterShort -= 1;
                return;
            }
            Draw.Dot(this, primaryLabel + Convert.ToString(CurrentBars[0]), false, 0, (Close[0]), Brushes.Yellow);
            lastEntryTime = Time[0];
            PrintOnce($"Entry_Base_Submit_{primaryLabel}_{CurrentBar}", $"{Time[0]}: Submitted base short entry: {primaryLabel}");

            // --- 2. Submit Scale-In Entry Orders ---
            EnterMultipleShortContracts(false);

            // --- 3. Set Initial Stop Losses (REMOVED FROM HERE) ---
            // SetStopLosses(primaryLabel); // <<< REMOVED

            // --- 4. Set Profit Targets (REMOVED FROM HERE) ---
            // SetProfitTargets(); // <<< REMOVED

            // --- 5. Send Discord Signal (Optional) ---
            // ... (Discord logic remains) ...
        }
        // ***** END OF MODIFIED SECTION *****

        #endregion
		
		#region Order Submission Helpers

		// This method encapsulates all order submissions and error handling.
		private Order SubmitEntryOrder(string orderLabel, OrderType orderType, int contracts)
		{
			Order submittedOrder = null;

			lock (orderLock)
			{
				if (!CanSubmitOrder())
				{
					Print (string.Format("{0}: Cannot submit {1} order: Minimum order interval not met.", Time[0], orderLabel));
					return null; // Or throw an exception if order submission is absolutely critical
				}

				try
				{
					switch (orderType)
					{
						case OrderType.Market:
							if (orderLabel == LE || orderLabel == QLE)
								submittedOrder = EnterLong(contracts, orderLabel);
							else if (orderLabel == SE || orderLabel == QSE)
								submittedOrder = EnterShort(contracts, orderLabel);
							else
								throw new ArgumentException("Invalid order label for Market order.");
							break;
						case OrderType.Limit:
							if (orderLabel == LE || orderLabel == QLE)
								submittedOrder = EnterLongLimit(contracts, GetCurrentBid() - LimitOffset * TickSize, orderLabel);
							else if (orderLabel == SE || orderLabel == QSE)
								submittedOrder = EnterShortLimit(contracts, GetCurrentAsk() + LimitOffset * TickSize, orderLabel);
							else
								throw new ArgumentException("Invalid order label for Limit order.");
							break;
						case OrderType.MIT:
							if (orderLabel == LE || orderLabel == QLE)
								submittedOrder = EnterLongMIT(contracts, GetCurrentBid() - LimitOffset * TickSize, orderLabel);
							else if (orderLabel == SE || orderLabel == QSE)
								submittedOrder = EnterShortMIT(contracts, GetCurrentAsk() + LimitOffset * TickSize, orderLabel);
							else
								throw new ArgumentException("Invalid order label for MIT order.");
							break;
						case OrderType.StopLimit:
							if (orderLabel == LE || orderLabel == QLE)
								submittedOrder = EnterLongLimit(contracts, GetCurrentBid() - LimitOffset * TickSize, orderLabel);
							else if (orderLabel == SE || orderLabel == QSE)
								submittedOrder = EnterShortLimit(contracts, GetCurrentAsk() + LimitOffset * TickSize, orderLabel);
							else
								throw new ArgumentException("Invalid order label for StopLimit order.");
							break;
						case OrderType.StopMarket:
							if (orderLabel == LE || orderLabel == QLE)
								submittedOrder = EnterLong(contracts, orderLabel);
							else if (orderLabel == SE || orderLabel == QSE)
								submittedOrder = EnterShort(contracts, orderLabel);
							else
								throw new ArgumentException("Invalid order label for StopMarket order.");
							break;
						default:
							throw new ArgumentOutOfRangeException(nameof(orderType), orderType, "Unsupported order type");
					}

					if (submittedOrder != null)
					{
						activeOrders[orderLabel] = submittedOrder;  // TRACK THE ORDER!
						lastOrderActionTime = DateTime.Now;
						Print (string.Format("{0}: Submitted {1} order with OrderId: {2}", Time[0], orderLabel, submittedOrder.OrderId));
					}
					else
					{
						Print (string.Format("{0}: Error: {1} Entry order was null after submission.", Time[0], orderLabel));
						orderErrorOccurred = true;
					}
				}
				catch (Exception ex)
				{
					Print (string.Format("{0}: Error submitting {1} entry order: {2}", Time[0], orderLabel, ex.Message));
					orderErrorOccurred = true;
				}
			}

			return submittedOrder;
		}

		private void SubmitExitOrder(string orderLabel)
		{
			lock(orderLock)
			{
				try
				{
					if (orderLabel == LE || orderLabel == QLE || orderLabel == Add1LE) {
						ExitLong(orderLabel);
					} else if(orderLabel == SE || orderLabel == QSE || orderLabel == Add1SE){
						ExitShort(orderLabel);
					} else {
						Print ($"Error: invalid order label {orderLabel}");
					}
					
					if(!activeOrders.ContainsKey(orderLabel))
						Print ($"Cannot cancel order that does not exist");
					
					if(activeOrders.TryGetValue(orderLabel, out Order orderToCancel)) {
						CancelOrder(orderToCancel);
						activeOrders.Remove(orderLabel);
					}
				} catch(Exception ex) {
					Print ($"Error submitting Exit order: {ex.Message}");
					orderErrorOccurred = true;
				}
			}
		}

		#endregion
		
		#region Rogue Order Detection

		private void ReconcileAccountOrders()
		{
		    lock (orderLock)
		    {
		        try
		        {
		            // Get all accounts
		            var accounts = Account.All;

		            if (accounts == null || accounts.Count == 0)
		            {
		                Print(string.Format("{0}: No accounts found.", Time[0]));
		                return;
		            }

		            // Iterate through the accounts and reconcile orders
		            foreach (Account account in accounts)
		            {
		                // Get the list of all orders associated with each instrument in that account
		                List<Order> accountOrders = new List<Order>();

		                try
		                {
		                    foreach (Position position in account.Positions)
		                    {
		                        Instrument instrument = position.Instrument;
		                        foreach (Order order in Orders)
		                        {
		                            if (order.Instrument == instrument && order.Account == account)
		                            {
		                                accountOrders.Add(order);
		                            }
		                        }
		                    }
		                }
		                catch (Exception ex)
		                {
		                    Print(string.Format("{0}: Error getting orders for account {1}: {2}", Time[0], account.Name, ex.Message));
		                    continue; // Move to the next account. Don't halt the entire strategy if one account fails.
		                }

		                // Check for nulls and validity of account orders
		                if (accountOrders == null || accountOrders.Count == 0)
		                {
		                    Print(string.Format("{0}: No orders found in account {1}.", Time[0], account.Name));
		                    continue; //Move to the next account
		                }

						// Create a list of order IDs from activeOrders
						HashSet<string> strategyOrderIds = new HashSet<string>(activeOrders.Values.Select(o => o.OrderId));

						// Iterate through the account orders and check if they are tracked by the strategy
						foreach (Order accountOrder in accountOrders)
						{
							// Use null conditional operator for more succinct code
							if (!strategyOrderIds.Contains(accountOrder?.OrderId))
							{
								// This is a rogue order!
								Print(string.Format("{0}: Rogue order detected! Account: {6} OrderId: {1}, OrderType: {2}, OrderStatus: {3}, Quantity: {4}, AveragePrice: {5}",
									Time[0], accountOrder.OrderId, accountOrder.OrderType, accountOrder.OrderState, accountOrder.Quantity, accountOrder.AverageFillPrice, account.Name));

								// You can either attempt to manage it:

								// Attempt to cancel the rogue order.  If it's a manual order, you might want to skip this step and just log it.
								try
								{
									CancelOrder(accountOrder);
									Print(string.Format("{0}: Attempted to cancel rogue order: {1}", Time[0], accountOrder.OrderId));
								}
								catch (Exception ex)
								{
									Print(string.Format("{0}: Failed to Cancel rogue order. Account: {6} OrderId: {1}, OrderType: {2}, OrderStatus: {3}, Quantity: {4}, AveragePrice: {5}, Reason: {7}",
										Time[0], accountOrder.OrderId, accountOrder.OrderType, accountOrder.OrderState, accountOrder.Quantity, accountOrder.AverageFillPrice, account.Name, ex.Message));
								}
							}
						}
		            } // End of account iteration
		        }
		        catch (Exception ex)
		        {
		            Print(string.Format("{0}: Error during account reconciliation: {1}", Time[0], ex.Message));
		            orderErrorOccurred = true;  // Consider whether to halt trading
		        }
		    }
		}

		#endregion
		
		#region Can Submit Order

		// Method to check the minimum interval between order submissions
		private bool CanSubmitOrder()
		{
			return (DateTime.Now - lastOrderActionTime) >= minOrderActionInterval;
		}
	
		#endregion
		
        #region OnExecutionUpdate

        // ***** MODIFIED SECTION *****
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price,
                                           int quantity, MarketPosition marketPosition, string orderId,
                                           DateTime time)
		{
            // Process base class OnExecutionUpdate FIRST if it exists (unlikely here but good practice)
            // base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);

		    // --- Handle Fills for Stop/Target Placement ---
		    if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
            {
                string fromEntrySignal = execution.Order.FromEntrySignal; // Get the label of the filled entry

                // Check if the filled order's label corresponds to one of our ENTRY signals
                bool isEntryFill = !string.IsNullOrEmpty(fromEntrySignal) &&
                                   (fromEntrySignal.StartsWith(LE) || fromEntrySignal.StartsWith(SE) ||
                                    fromEntrySignal.StartsWith(QLE) || fromEntrySignal.StartsWith(QSE) ||
                                    fromEntrySignal.StartsWith(Add1LE) || fromEntrySignal.StartsWith(Add1SE));
                                    // Add LE2/SE2 etc. if they should trigger independent stops - unlikely with current logic

                if (isEntryFill)
                {
                    PrintOnce($"OnExec_EntryFill_{fromEntrySignal}_{executionId}", $"{time}: ENTRY FILL detected for label '{fromEntrySignal}' @ {price}. Qty: {quantity}. Placing Stop/Target.");

                    // Use a lock to prevent race conditions if multiple fills come in rapidly (unlikely but safe)
                    lock (orderLock)
                    {
                        try
                        {
                            // --- Place Initial Stop Loss based on this specific fill ---
                            // Pass the actual execution price and the signal label
                            SetInitialStopLossOnFill(fromEntrySignal, price);

                            // --- Place Profit Target(s) based on this specific fill ---
                            // Pass the actual execution price and the signal label
                            SetInitialProfitTargetOnFill(fromEntrySignal, price);

                            // Optional: Send Discord signal AFTER stop/target placement attempts
                            // SendDiscordSignalOnFill(execution);
                        }
                        catch (Exception ex)
                        {
                             Print($"CRITICAL ERROR in OnExecutionUpdate Stop/Target Placement for {fromEntrySignal}: {ex.Message} --- StackTrace: {ex.StackTrace}");
                             orderErrorOccurred = true; // Halt strategy if placement fails critically
                        }
                    }
                }
            }

            // --- Handle Existing Order Tracking Logic ---
            // (Keep the part that removes orders from the activeOrders dictionary on Fill/Cancel/Reject)
		    lock (orderLock)
		    {
		        string orderLabel = activeOrders.FirstOrDefault(x => x.Value.OrderId == orderId).Key;

		        if (!string.IsNullOrEmpty(orderLabel))
		        {
		            switch (execution.Order.OrderState)
		            {
		                case OrderState.Filled:
		                case OrderState.Cancelled:
		                case OrderState.Rejected:
                            // Print message is handled above for fills, maybe add for Cancel/Reject
                            if(execution.Order.OrderState != OrderState.Filled)
                                Print($"{Time[0]}: Order {orderId} ({orderLabel}) is {execution.Order.OrderState}. Removing from active tracking.");
		                    activeOrders.Remove(orderLabel);
		                    break;
		                default:
                            // Optional: Log other state changes if needed
		                    // Print($"{Time[0]}: Order {orderId} ({orderLabel}) updated to state: {execution.Order.OrderState}");
		                    break;
		            }
		        }
		        else if (execution.Order != null && execution.Order.OrderState != OrderState.Unknown && !execution.Order.IsLimit && !execution.Order.IsStopMarket) // Avoid logging updates for working stops/targets placed by strategy
		        {
		            // Log execution updates for orders NOT tracked by our entry dictionary (could be stops/targets filling, or rogue)
		            Print($"{Time[0]}: Execution update for untracked order {orderId} ({execution.Order.Name}). State: {execution.Order.OrderState}, Price: {price}, Qty: {quantity}");
                    // Note: Don't automatically cancel here, as it could be a legitimate stop/target fill. Rogue detection handles cancels.
		        }
		    }
            // --- End Existing Order Tracking ---
		}

		#endregion

        // ***** NEW HELPER METHODS for OnExecutionUpdate *****

        #region Initial Stop/Target Placement on Fill

        // Places the initial stop loss immediately after an entry fill
        private void SetInitialStopLossOnFill(string filledEntrySignal, double executionPrice)
        {
            if (Position.MarketPosition == MarketPosition.Flat) // Should not happen if fill just occurred, but safety check
            {
                PrintOnce($"SetInitialStop_Flat_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: Cannot set initial stop for {filledEntrySignal}, position reported flat immediately after fill.");
                return;
            }
            if (TickSize <= 0) { PrintOnce($"SetInitialStop_TickSize_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: Cannot set initial stop for {filledEntrySignal}. Invalid TickSize."); return; }

            // --- Check for Override Condition ---
            bool useRegChanOverride = (TrailStopType == TrailStopTypeKC.Regression_Channel_Trail && EnableRegChanProfitTarget);
            int effectiveInitialStopTicks = useRegChanOverride ? 120 : InitialStop;

             // --- Calculate Initial Stop Price using executionPrice ---
            double initialStopPrice = (Position.MarketPosition == MarketPosition.Long)
                                     ? executionPrice - (effectiveInitialStopTicks * TickSize)
                                     : executionPrice + (effectiveInitialStopTicks * TickSize);

            PrintOnce($"SetInitialStop_Calc_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Calculated initial stop PRICE: {initialStopPrice:F5} for label {filledEntrySignal} (Exec: {executionPrice:F5}, Ticks: {effectiveInitialStopTicks}).");

            // --- Validate Stop Price ---
             if (!IsValidStopPlacement(initialStopPrice, Position.MarketPosition))
            {
                 PrintOnce($"SetInitialStop_Invalid_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Initial stop placement {initialStopPrice:F5} is invalid for '{filledEntrySignal}'. Skipping placement.");
                 return;
            }

            // --- Apply Stop using Explicit Exit Order ---
            // Use SetFixedStopLoss helper which now uses Exit...StopMarket
             try
             {
                 SetFixedStopLoss(filledEntrySignal, CalculationMode.Price, initialStopPrice, false);
                 string modeInfo = enableTrail ? "(Will Trail)" : "(Fixed)"; // Indicate intent
                 PrintOnce($"SetInitialStop_Apply_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Applied initial stop via Exit...StopMarket(Price: {initialStopPrice:F5}) {modeInfo} for label {filledEntrySignal}.");
             }
             catch (Exception ex)
             {
                 Print($"ERROR Setting Initial Stop on Fill for {filledEntrySignal}: {ex.Message}");
                 orderErrorOccurred = true;
             }
        }

        // Places the initial profit target(s) immediately after an entry fill
        private void SetInitialProfitTargetOnFill(string filledEntrySignal, double executionPrice)
        {
            if (isFlat || TickSize <= 0) return; // Basic safety checks

            // Determine the base label and scaled labels based on the filled signal
            string baseLabel = "";
            string labelPrefix = "";
            bool isQuickEntry = false;

            if (filledEntrySignal.StartsWith(LE) || filledEntrySignal.StartsWith(QLE)) {
                isQuickEntry = filledEntrySignal.StartsWith(QLE);
                baseLabel = isQuickEntry ? QLE : LE;
                labelPrefix = isQuickEntry ? "QLE" : LE;
            } else if (filledEntrySignal.StartsWith(SE) || filledEntrySignal.StartsWith(QSE)) {
                 isQuickEntry = filledEntrySignal.StartsWith(QSE);
                 baseLabel = isQuickEntry ? QSE : SE;
                 labelPrefix = isQuickEntry ? "QSE" : SE;
            } else if (filledEntrySignal.StartsWith(Add1LE) || filledEntrySignal.StartsWith(Add1SE)) {
                 // Decide how to handle targets for Add1 entries - often they don't get their own target
                 PrintOnce($"SetInitialPT_Add1_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Skipping specific profit target for Add1 entry {filledEntrySignal}.");
                 return;
            }
            else {
                 PrintOnce($"SetInitialPT_UnknownLabel_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Cannot determine profit target logic for unknown entry label {filledEntrySignal}.");
                 return; // Unknown entry label
            }


            // --- Route based on active Profit Target Mode ---
            if (EnableRegChanProfitTarget)
            {
                // Calculate RegChan Target (can reuse existing helper, but needs executionPrice)
                SetRegChanProfitTargetOnFill(executionPrice, filledEntrySignal); // Pass execution price
            }
            else if (EnableFixedProfitTarget)
            {
                // Apply fixed targets based on Ticks from executionPrice
                SetFixedProfitTargetsOnFill(executionPrice, baseLabel, labelPrefix);
            }
            else if (EnableDynamicProfitTarget)
            {
                // Dynamic Pivot Target (usually set based on market conditions, maybe less critical here unless entry is far from pivots)
                // For simplicity, we might just apply the standard fixed target as a fallback, or recalculate based on pivots vs execution price
                 PrintOnce($"SetInitialPT_DynamicInfo_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Dynamic Profit Target mode active. Re-evaluating standard PT placement for {filledEntrySignal}.");
                 SetDynamicPivotProfitTargets(); // Re-call the main method which checks market conditions
            }
        }

        // Helper specifically for setting fixed targets based on execution price
        private void SetFixedProfitTargetsOnFill(double execPrice, string baseLbl, string prefix)
        {
             try
            {
                // Apply base target (using ticks from execution price)
                if(ProfitTarget > 0) SetProfitTarget(baseLbl, CalculationMode.Ticks, ProfitTarget);

                // Apply scaled targets if enabled (using ticks from execution price)
                if(EnableProfitTarget2 && ProfitTarget2 > 0) SetProfitTarget(prefix + "2", CalculationMode.Ticks, ProfitTarget2);
                if(EnableProfitTarget3 && ProfitTarget3 > 0) SetProfitTarget(prefix + "3", CalculationMode.Ticks, ProfitTarget3);
                if(EnableProfitTarget4 && ProfitTarget4 > 0) SetProfitTarget(prefix + "4", CalculationMode.Ticks, ProfitTarget4);

                 PrintOnce($"SetInitialPT_Fixed_{baseLbl}_{CurrentBar}", $"{Time[0]}: [On Fill] Applied fixed profit target(s) relative to exec price {execPrice:F5} for base label {baseLbl}.");
            }
            catch (Exception ex)
            {
                Print($"Error setting Fixed Profit Targets on Fill for {baseLbl}: {ex.Message}");
                orderErrorOccurred = true;
            }
        }

         // Helper specifically for setting RegChan targets based on execution price
        private void SetRegChanProfitTargetOnFill(double execPrice, string filledEntrySignal)
        {
             if (CurrentBar < RegChanPeriod - 1 || TickSize <= 0) return;

            // --- Check for Override Condition ---
            bool useRegChanOverride = (TrailStopType == TrailStopTypeKC.Regression_Channel_Trail && EnableRegChanProfitTarget);
            int effectiveFallbackProfitTargetTicks = useRegChanOverride ? 60 : (int)ProfitTarget;

            double targetPrice = 0;
            string targetBandName = "";
            bool useFallbackTarget = false;

            if (Position.MarketPosition == MarketPosition.Long) { targetPrice = RegressionChannel2.Upper[1]; targetBandName = "Upper"; } // Use RegChan2 for targets usually
            else if (Position.MarketPosition == MarketPosition.Short) { targetPrice = RegressionChannel2.Lower[1]; targetBandName = "Lower"; }
            else return;

            double priceDifference = (Position.MarketPosition == MarketPosition.Long) ? (targetPrice - execPrice) : (execPrice - targetPrice);
            double targetTicksDouble = priceDifference / TickSize;

            if (targetTicksDouble < MinRegChanTargetDistanceTicks) useFallbackTarget = true;

            int targetTicks;
            if (useFallbackTarget) {
                targetTicks = effectiveFallbackProfitTargetTicks;
                if (targetTicks < 1) targetTicks = 1;
            } else {
                targetTicks = (int)Math.Max(1.0, Math.Round(targetTicksDouble, MidpointRounding.AwayFromZero));
            }

             // Apply to the specific filled entry signal label
            try
            {
                 PrintOnce($"SetInitialPT_RegChan_{filledEntrySignal}_{CurrentBar}", $"{Time[0]}: [On Fill] Setting RegChan PT ({targetTicks} ticks, Fallback: {useFallbackTarget}) for label: {filledEntrySignal}");
                 SetProfitTarget(filledEntrySignal, CalculationMode.Ticks, targetTicks);
                 // If scaling, need to decide if *all* targets go here or just the one matching the fill label
                 // Current logic assumes SetProfitTarget handles the 'fromEntrySignal' association correctly.
            }
            catch (Exception ex) { HandleSetTargetError(filledEntrySignal, useFallbackTarget ? "Fallback" : "RegChan", ex); }
        }


        #endregion

        // ***** END NEW HELPER METHODS *****
		
		#region Pivot Profit Targets

        private void SetProfitTargetBasedOnLongConditions()
        {
            if (Close[0] > s3 && Low[0] <= s3)
				SetProfitTarget(LE, CalculationMode.Price, s3m);
			else if (Close[0] > s3m && Low[0] <= s3m)
				SetProfitTarget(LE, CalculationMode.Price, s2);
			else if (Close[0] > s2 && Low[0] <= s2)
				SetProfitTarget(LE, CalculationMode.Price, s2m);
			else if (Close[0] > s2m && Low[0] <= s2m)	
				SetProfitTarget(LE, CalculationMode.Price, s1);
			else if (Close[0] > s1 && Low[0] <= s1)
				SetProfitTarget(LE, CalculationMode.Price, s1m);
			else if (Close[0] > s1m && Low[0] <= s1m)
				SetProfitTarget(LE, CalculationMode.Price, pivotPoint);
			else if (Close[0] > pivotPoint && Low[0] <= pivotPoint)
				SetProfitTarget(LE, CalculationMode.Price, r1m);
			else if (Close[0] > r1m && Low[0] <= r1m)
				SetProfitTarget(LE, CalculationMode.Price, r1);
			else if (Close[0] > r1 && Low[0] <= r1)
				SetProfitTarget(LE, CalculationMode.Price, r2m);
			else if (Close[0] > r2m && Low[0] <= r2m)
				SetProfitTarget(LE, CalculationMode.Price, r2);
			else if (Close[0] > r2 && Low[0] <= r2)
				SetProfitTarget(LE, CalculationMode.Price, r3m);
			else if (Close[0] > r3m && Low[0] <= r3m)
				SetProfitTarget(LE, CalculationMode.Price, r3);
			else if (Close[0] > r3 && Low[0] <= r3)
				SetProfitTarget(@LE, CalculationMode.Ticks, ProfitTarget);
        }

        private void SetProfitTargetBasedOnShortConditions()
        {
            if (Close[0] < r3 && High[0] >= r3)
				SetProfitTarget(SE, CalculationMode.Price, r3m);
			else if (Close[0] < r3m && High[0] >= r3m)
				SetProfitTarget(SE, CalculationMode.Price, r2);
			else if (Close[0] < r2 && High[0] >= r2)
				SetProfitTarget(SE, CalculationMode.Price, r2m);
			else if (Close[0] < r2m && High[0] >= r2m)
				SetProfitTarget(SE, CalculationMode.Price, r1);
			else if (Close[0] < r1 && High[0] >= r1)
				SetProfitTarget(SE, CalculationMode.Price, r1m);
			else if (Close[0] < r1m && High[0] >= r1m)
				SetProfitTarget(SE, CalculationMode.Price, pivotPoint);
			else if (Close[0] < pivotPoint && High[0] >= pivotPoint)
				SetProfitTarget(SE, CalculationMode.Price, s1m);
			else if (Close[0] < s1m && High[0] >= s1m)
				SetProfitTarget(SE, CalculationMode.Price, s1);
			else if (Close[0] < s1 && High[0] >= s1)
				SetProfitTarget(SE, CalculationMode.Price, s2m);
			else if (Close[0] < s2m && High[0] >= s2m)
				SetProfitTarget(SE, CalculationMode.Price, s2);
			else if (Close[0] < s2 && High[0] >= s2)
				SetProfitTarget(SE, CalculationMode.Price, s3m);
			else if (Close[0] < s3m && High[0] >= s3m)
				SetProfitTarget(SE, CalculationMode.Price, s3);
			else if (Close[0] < s3 && High[0] >= s3)
				SetProfitTarget(@SE, CalculationMode.Ticks, ProfitTarget);
        	}
		
		private void EnterMultipleLongContracts(bool isManual) {
			if (enableFixedProfitTarget) {
				if(isManual) {
					EnterMultipleOrders(true, EnableProfitTarget2, @"QLE2", Contracts2);
					EnterMultipleOrders(true, EnableProfitTarget3,  @"QLE3", Contracts3);
					EnterMultipleOrders(true, EnableProfitTarget4,  @"QLE4", Contracts4);
				} else {
					EnterMultipleOrders(true, EnableProfitTarget2, @LE2, Contracts2);
					EnterMultipleOrders(true, EnableProfitTarget3,  @LE3, Contracts3);
					EnterMultipleOrders(true, EnableProfitTarget4,  @LE4, Contracts4);
				}
			}
		}
		
		private void EnterMultipleShortContracts(bool isManual) {
			if (enableFixedProfitTarget) {
				if(isManual) {
					EnterMultipleOrders(false, EnableProfitTarget2, @"QSE2", Contracts2);
					EnterMultipleOrders(false, EnableProfitTarget3,  @"QSE3", Contracts3);
					EnterMultipleOrders(false, EnableProfitTarget4,  @"QSE4", Contracts4);
				} else {
					EnterMultipleOrders(false, EnableProfitTarget2, @SE2, Contracts2);
					EnterMultipleOrders(false, EnableProfitTarget3,  @SE3, Contracts3);
					EnterMultipleOrders(false, EnableProfitTarget4,  @SE4, Contracts4);
				}
			}
		}
		
		private void EnterMultipleOrders(bool isLong, bool isEnableTarget, string signalName, int contracts)
		{
		    if (isEnableTarget)
		    {
		        if (isLong)
		        {
		            if (OrderType == OrderType.Market)
		                EnterLong(Convert.ToInt32(contracts), signalName);
		            else if (OrderType == OrderType.Limit)
		                EnterLongLimit(Convert.ToInt32(contracts), GetCurrentBid() - LimitOffset * TickSize, signalName);
		            else if (OrderType == OrderType.MIT)
		                EnterLongMIT(Convert.ToInt32(contracts), GetCurrentBid() - LimitOffset * TickSize, signalName);
		            else if (OrderType == OrderType.StopLimit)
		                EnterLongLimit(Convert.ToInt32(contracts), GetCurrentBid() - LimitOffset * TickSize, signalName);
		            else if (OrderType == OrderType.StopMarket)
		                EnterLong(Convert.ToInt32(contracts), signalName);
		        }
		        else
		        {
		            if (OrderType == OrderType.Market)
		                EnterShort(Convert.ToInt32(contracts), signalName);
		            else if (OrderType == OrderType.Limit)
		                EnterShortLimit(Convert.ToInt32(contracts), GetCurrentAsk() + LimitOffset * TickSize, signalName);
		            else if (OrderType == OrderType.MIT)
		                EnterShortMIT(Convert.ToInt32(contracts), GetCurrentAsk() + LimitOffset * TickSize, signalName);
		            else if (OrderType == OrderType.StopLimit)
		                EnterShortLimit(Convert.ToInt32(contracts), GetCurrentAsk() + LimitOffset * TickSize, signalName);
		            else if (OrderType == OrderType.StopMarket)
		                EnterShort(Convert.ToInt32(contracts), signalName);
		        }
		    }
		}
		
		#endregion		
		
		#region Set Profit Targets

        private void SetProfitTargets()
        {
            // --- Exit if flat ---
            if (isFlat) return;

            // --- Get Entry Price and Validate ---
            double entryPrice = Position.AveragePrice;
            if (entryPrice == 0 || TickSize <= 0)
            {
                // Print warning only once if values are invalid
                if (entryPrice == 0)
                    // FIX: Add the message string back as the second argument
                    PrintOnce("PT_Skip_EntryZero", $"{Time[0]}: SetProfitTargets - Skipping, Position.AveragePrice is 0.");
                if (TickSize <= 0)
                     // FIX: Add the message string back as the second argument
                    PrintOnce("PT_Skip_TickSizeInvalid", $"{Time[0]}: SetProfitTargets - Skipping, TickSize is invalid ({TickSize}).");
                return;
            }

            // === Route based on active Profit Target Mode ===

            if (EnableRegChanProfitTarget)
            {
                SetRegChanProfitTarget(entryPrice); // Call helper for RegChan logic
            }
            else if (EnableFixedProfitTarget)
            {
                SetFixedProfitTargets(); // Call helper for Fixed logic
            }
            else if (EnableDynamicProfitTarget)
            {
                SetDynamicPivotProfitTargets(); // Call helper for Dynamic logic
            }
            // Optional: Add an else block to print a warning if no mode is selected,
            // though the property setters should prevent this.
            // else { Print($"{Time[0]}: SetProfitTargets - Warning: No profit target mode enabled."); }
        }

        // --- Helper Method for Regression Channel Target ---
        private void SetRegChanProfitTarget(double entryPrice)
        {
            if (CurrentBar < RegChanPeriod - 1) return; // Channel ready check

            // --- Check for Override Condition ---
            bool useRegChanOverride = (TrailStopType == TrailStopTypeKC.Regression_Channel_Trail && EnableRegChanProfitTarget);
            int effectiveFallbackProfitTargetTicks = useRegChanOverride ? 60 : (int)ProfitTarget; // Use 60 if override active

            if (useRegChanOverride)
                 PrintOnce($"PT_RegChan_OverridePT_{CurrentBar}", $"{Time[0]}: RegChan Trail & Target active. Using OVERRIDE Fallback ProfitTarget = {effectiveFallbackProfitTargetTicks} ticks.");


            double targetPrice = 0;
            string targetBandName = "";
            bool useFallbackTarget = false; // Flag to indicate fallback

            if (isLong) { targetPrice = RegressionChannel2.Upper[1]; targetBandName = "Upper"; }
            else if (isShort) { targetPrice = RegressionChannel2.Lower[1]; targetBandName = "Lower"; }
            else return; // Not in position

            double priceDifference = isLong ? (targetPrice - entryPrice) : (entryPrice - targetPrice);
            double targetTicksDouble = priceDifference / TickSize; // Calculate potential ticks from channel

            // --- Fallback Check ---
            if (targetTicksDouble < MinRegChanTargetDistanceTicks)
            {
                 PrintOnce("PT_RegChan_Fallback", $"{Time[0]}: RegChan target ({targetBandName} band {targetPrice:F5} / {targetTicksDouble:F1} ticks) too close (Min: {MinRegChanTargetDistanceTicks}). Falling back to effective ProfitTarget ({effectiveFallbackProfitTargetTicks} ticks).");
                 useFallbackTarget = true;
            }
            // --- End Fallback Check ---

            int targetTicks;
            if (useFallbackTarget)
            {
                targetTicks = effectiveFallbackProfitTargetTicks; // Use the effective fallback value
                if (targetTicks < 1)
                {
                    PrintOnce("PT_Fallback_Zero", $"{Time[0]}: Fallback ProfitTarget is < 1 ({effectiveFallbackProfitTargetTicks}). Setting target to 1 tick.");
                    targetTicks = 1;
                }
            }
            else
            {
                // Use channel target if distance is sufficient
                targetTicks = (int)Math.Max(1.0, Math.Round(targetTicksDouble, MidpointRounding.AwayFromZero));
            }

            List<string> labels = GetRelevantOrderLabels();
            if (labels.Count == 0) return;

            // Update log message based on which target is used
            if (!useFallbackTarget)
                PrintOnce("PT_RegChan_Setting", $"{Time[0]}: Setting RegChan {targetBandName} ({targetPrice:F5}) target ({targetTicks} ticks) for labels: {string.Join(", ", labels)}");
            else
                 PrintOnce("PT_Fallback_Setting", $"{Time[0]}: Setting Fallback Profit Target ({targetTicks} ticks) for labels: {string.Join(", ", labels)}");

            foreach (string label in labels)
            {
                try { SetProfitTarget(label, CalculationMode.Ticks, targetTicks); }
                catch (Exception ex) { HandleSetTargetError(label, useFallbackTarget ? "Fallback" : "RegChan", ex); }
            }
        }

        // --- Helper Method for Fixed Targets ---
        private void SetFixedProfitTargets()
        {
            try
            {
                // Apply base target first
                string baseLabel = isLong ? (quickLongBtnActive ? QLE : LE) : (quickShortBtnActive ? QSE : SE);
                SetProfitTargetForLabel(baseLabel, ProfitTarget, true); // Using main ProfitTarget parameter

                // Apply scaled targets if enabled
                string prefix = isLong ? (quickLongBtnActive ? "QLE" : LE.Substring(0,2)) : (quickShortBtnActive ? "QSE" : SE.Substring(0,2)); // LE or SE or QLE or QSE

                SetProfitTargetForLabel(prefix + "2", ProfitTarget2, EnableProfitTarget2);
                SetProfitTargetForLabel(prefix + "3", ProfitTarget3, EnableProfitTarget3);
                SetProfitTargetForLabel(prefix + "4", ProfitTarget4, EnableProfitTarget4);
            }
            catch (Exception ex)
            {
                Print($"{Time[0]}: Error in SetFixedProfitTargets: {ex.Message}");
                orderErrorOccurred = true;
            }
        }

        // --- Helper Method for Dynamic Pivot Targets ---
        private void SetDynamicPivotProfitTargets()
        {
            if (isLong)
            {
                if (Close[0] > r3 && Low[0] <= r3)
                    SetProfitTargetForLabel(@LE, ProfitTarget, true); // Fallback to fixed Ticks if above R3
                else
                    SetProfitTargetBasedOnLongConditions(); // Sets price targets based on pivots
            }
            else if (isShort)
            {
                if (Close[0] < s3 && High[0] >= s3)
                    SetProfitTargetForLabel(@SE, ProfitTarget, true); // Fallback to fixed Ticks if below S3
                else
                    SetProfitTargetBasedOnShortConditions(); // Sets price targets based on pivots
            }
        }


        // --- Modified Helper to Apply Target by Ticks ---
		private void SetProfitTargetForLabel(string label, double profitTargetTicks, bool isEnabled)
		{
		    if (isEnabled && profitTargetTicks > 0) // Ensure ticks > 0
		    {
		        try
                {
		            SetProfitTarget(label, CalculationMode.Ticks, profitTargetTicks);
                }
                catch (Exception ex) { HandleSetTargetError(label, "Fixed/Dynamic", ex); }
		    }
		}

        // --- Error Handler Helper ---
        private void HandleSetTargetError(string label, string type, Exception ex)
        {
           PrintOnce($"PT_Error_{label}_{type}", $"{Time[0]}: Error setting {type} Profit Target for label '{label}': {ex.Message}");
           orderErrorOccurred = true;
        }

        /// <summary>
        /// Prints a message to the NinjaScript output window only once per bar for a given key.
        /// </summary>
        /// <param name="key">A unique identifier for the specific message type.</param>
        /// <param name="message">The message string to print.</param>
        protected void PrintOnce(string key, string message)
        {
            // Check if Bars is initialized and we have a valid CurrentBar
            if (Bars == null || Bars.Count == 0 || CurrentBar < 0)
            {
                Print($"PrintOnce WARNING: Cannot track message key '{key}' - Bars not ready. Message: {message}");
                return; // Cannot track without bar context
            }

            int lastPrintedBar;
            // Check if the key exists and if it was printed on the current bar
            if (!printedMessages.TryGetValue(key, out lastPrintedBar) || lastPrintedBar != CurrentBar)
            {
                // If not printed yet on this bar, print it and update the dictionary
                Print(message); // Use the standard Print method
                printedMessages[key] = CurrentBar; // Store the current bar number for this key
            }
            // If already printed on this bar, do nothing
        }
        #endregion
		
		#region Stop Adjustment (Manual Buttons)

        // Adjusts the active trailing stop by a specified number of ticks
		protected void AdjustStopLoss(int tickAdjustment)
		{
            // --- Pre-checks ---
		    if (isFlat)
		    {
		        Print($"{Time[0]}: AdjustStopLoss: No active position.");
		        return;
		    }
            if (tickAdjustment == 0)
            {
                Print($"{Time[0]}: AdjustStopLoss: Tick adjustment is zero, no change needed.");
                return;
            }
            // Ensure trailing is conceptually enabled for adjustment
            if (!enableTrail && !enableFixedStopLoss) // Only adjust if some stop mechanism is active
            {
                 Print($"{Time[0]}: AdjustStopLoss: Neither Trailing nor Fixed Stop enabled.");
                 return;
            }

            // --- Get Current State ---
		    double entryPrice = Position.AveragePrice;
            if (entryPrice == 0) { Print($"{Time[0]}: AdjustStopLoss: Cannot adjust, entry price is 0."); return; } // Safety check
		    bool isLong = Position.MarketPosition == MarketPosition.Long;
		    double currentMarketPrice = Close[0];

            // --- Determine Current Stop Price ---
            // Find the current stop price level to adjust FROM.
            // We need to check active orders OR infer from current settings if no order found.
            double currentStopPrice = 0;
            Order workingStop = Orders.FirstOrDefault(o => o.OrderState == OrderState.Working && o.IsStopMarket); // Find *any* working stop

            if (workingStop != null)
            {
                currentStopPrice = workingStop.StopPrice;
                Print($"{Time[0]}: AdjustStopLoss: Found working stop order {workingStop.OrderId} at price {currentStopPrice:F5}.");
            }
            else
            {
                // No working stop order found. Infer based on current mode.
                Print($"{Time[0]}: AdjustStopLoss: No working stop order found. Inferring current stop level...");
                if (enableFixedStopLoss)
                {
                    currentStopPrice = isLong ? entryPrice - (InitialStop * TickSize) : entryPrice + (InitialStop * TickSize);
                    Print($"{Time[0]}: AdjustStopLoss: Inferred from Fixed Stop setting. Current Stop Price: {currentStopPrice:F5}");
                }
                else if (enableTrail)
                {
                    // Need to calculate the effective stop price based on the *current* trail mode/value
                    double currentTrailTicks = CalculateTrailingStopTicks(); // Use the helper!
                    currentStopPrice = isLong ? currentMarketPrice - (currentTrailTicks * TickSize) : currentMarketPrice + (currentTrailTicks * TickSize);
                    // Note: This inferred price for trailing might slightly differ from an actual working order due to timing.
                     Print($"{Time[0]}: AdjustStopLoss: Inferred from Trailing Stop setting ({currentTrailTicks} ticks). Current Stop Price: {currentStopPrice:F5}");
                }
                else
                {
                     Print($"{Time[0]}: AdjustStopLoss: Cannot determine current stop price level. Aborting.");
                     return;
                }
            }

             if (currentStopPrice == 0) { Print($"{Time[0]}: AdjustStopLoss: Could not determine a valid current stop price. Aborting."); return; } // Final check

            // --- Calculate New Target Stop Price ---
		    double newTargetStopPrice = isLong
		        ? currentStopPrice + tickAdjustment * TickSize  // Move towards market for longs
		        : currentStopPrice - tickAdjustment * TickSize;  // Move towards market for shorts

            Print($"{Time[0]}: AdjustStopLoss: Current Stop: {currentStopPrice:F5}, Tick Adj: {tickAdjustment}, New Target Stop: {newTargetStopPrice:F5}");

            // --- Validate New Stop Price ---
            // Prevent moving stop TO or BEYOND the current market price
		    if ((isLong && newTargetStopPrice >= currentMarketPrice) || (!isLong && newTargetStopPrice <= currentMarketPrice))
		    {
		        Print($"{Time[0]}: AdjustStopLoss: Cannot move stop. New target price {newTargetStopPrice:F5} invalid relative to current market price {currentMarketPrice:F5}.");
		        return; // Do not proceed
		    }
            // Optional: Prevent moving stop TO or BEYOND the entry price if it's not desired (e.g., preventing moving BE back into loss)
            // if ((isLong && newTargetStopPrice < entryPrice) || (!isLong && newTargetStopPrice > entryPrice))
            // {
            //      Print($"{Time[0]}: AdjustStopLoss: Cannot move stop beyond entry price {entryPrice:F5}.");
            //      return;
            // }


            // --- Calculate Tick Offset for SetTrailStop (from Entry Price) ---
            // This determines how many ticks behind the *entry* price the stop needs to be placed
            // to achieve the newTargetStopPrice. SetTrailStop(Mode=Ticks) always works relative to entry.
            double breakevenTicks = isLong
		        ? (entryPrice - newTargetStopPrice) / TickSize
		        : (newTargetStopPrice - entryPrice) / TickSize;

            Print($"{Time[0]}: AdjustStopLoss: Calculated Tick Offset From Entry: {breakevenTicks:F2}");

            // --- Sanity Check Offset ---
            // The offset must be positive for SetTrailStop(Mode=Ticks)
            if (breakevenTicks <= 0)
            {
                 Print($"{Time[0]}: AdjustStopLoss: Calculated non-positive breakevenTicks ({breakevenTicks:F2}). Aborting adjustment. Check logic or price validation.");
                 return; // Stop adjustment if offset is invalid
            }

            // --- Apply to Relevant Labels using Safe Helper ---
		    List<string> orderLabels = GetRelevantOrderLabels();
            if (orderLabels.Count == 0)
            {
                 Print($"{Time[0]}: AdjustStopLoss: No relevant order labels found to apply adjustment to.");
                 return;
            }

		    Print($"{Time[0]}: AdjustStopLoss: Applying adjustment (Offset: {breakevenTicks:F2} ticks from entry) to labels: {string.Join(", ", orderLabels)}");
		    foreach (string label in orderLabels)
		    {
                // Use the SAFE helper function
                // Mode is Ticks, value is offset from ENTRY, isSimulated=true to keep strategy managing trail
		        SetTrailingStop(label, CalculationMode.Ticks, breakevenTicks, true);
		    }
            ForceRefresh(); // Refresh chart UI if needed after manual adjustment
		}

        #endregion 	
 
		#region Move To Breakeven (Manual Buttons) // Keep methods in this region or similar

        // Manually moves the active trailing stop to the Breakeven level (+/- offset)
		protected void MoveToBreakeven()
		{
            // --- Pre-checks ---
		    if (isFlat)
            {
                 Print($"{Time[0]}: MoveToBreakeven: No active position.");
                 return;
            }
            // Ensure trailing is conceptually enabled, otherwise BE doesn't make sense
            if (!enableTrail && !enableFixedStopLoss)
            {
                 Print($"{Time[0]}: MoveToBreakeven: Neither Trailing nor Fixed Stop enabled.");
                 return;
            }

            // Optional: A stricter check might compare PnL directly if needed:
			double currentUnrealizedPnlTicks = Position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks, Close[0]);
			
            // --- Get Current State ---
            double entryPrice = Position.AveragePrice;
            if (entryPrice == 0) { Print($"{Time[0]}: MoveToBreakeven: Cannot adjust, entry price is 0."); return; } // Safety check
            bool isLong = Position.MarketPosition == MarketPosition.Long;
		    double currentMarketPrice = Close[0];

            // --- Calculate Target Breakeven Stop Price ---
            // Move stop to Entry Price +/- Offset ticks
		    double offsetPriceAdjustment = BE_Offset * TickSize;
			double targetBreakevenStopPrice = entryPrice + (isLong ? offsetPriceAdjustment : -offsetPriceAdjustment);
				
            Print($"{Time[0]}: MoveToBreakeven: Target BE Stop Price: {targetBreakevenStopPrice:F5} (Entry: {entryPrice:F5}, Offset Ticks: {BE_Offset})");

            // --- Validate New Stop Price ---
            // Prevent moving stop TO or BEYOND the current market price
		    if ((isLong && targetBreakevenStopPrice >= currentMarketPrice) || (!isLong && targetBreakevenStopPrice <= currentMarketPrice))
		    {
		        Print($"{Time[0]}: MoveToBreakeven: Cannot move stop. Target BE price {targetBreakevenStopPrice:F5} invalid relative to current market price {currentMarketPrice:F5}. Position might not be profitable enough.");
		        return; // Do not proceed
		    }
			
			if (currentUnrealizedPnlTicks < BE_Offset) { // Or maybe just < 0 ?
				Print($"{Time[0]}: MoveToBreakeven: Position not sufficiently profitable (PnL Ticks: {currentUnrealizedPnlTicks:F2} < Offset: {BE_Offset}).");
				return;
			}

			// Determine if breakeven conditions are met
			if (currentUnrealizedPnlTicks >= BE_Offset) 
			{	
	            // --- Calculate Tick Offset for SetTrailStop (from Entry Price) ---
	            double breakevenTicks = targetBreakevenStopPrice / TickSize;
	
	            Print($"{Time[0]}: MoveToBreakeven: Calculated Tick Offset From Entry: {breakevenTicks:F2}");
	
	            // --- Sanity Check Offset ---
	            if (breakevenTicks <= 0)
	            {
	                 Print($"{Time[0]}: MoveToBreakeven: Calculated non-positive breakevenTicks ({breakevenTicks:F2}). Aborting adjustment. Check logic/offset.");
	                 // This case implies the BE point is beyond the entry price in the direction of loss,
	                 // which should only happen with a negative BE_Offset.
	                 return;
	            }
	
	            // --- Apply to Relevant Labels using Safe Helper ---
			    List<string> orderLabels = GetRelevantOrderLabels();
	            if (orderLabels.Count == 0)
	            {
	                 Print($"{Time[0]}: MoveToBreakeven: No relevant order labels found to apply adjustment to.");
	                 return;
	            }

	            Print($"{Time[0]}: MoveToBreakeven: Applying adjustment (Offset: {breakevenTicks:F2} ticks from entry) to labels: {string.Join(", ", orderLabels)}");
			    foreach (string label in orderLabels)
			    {
			        SetTrailingStop(label, CalculationMode.Ticks, breakevenTicks, true);
			    }
	
	            // Mark breakeven as realized if using the flag for logic elsewhere
	            _beRealized = true; // Set flag after successful manual application
	            Print($"{Time[0]}: MoveToBreakeven: Manual Breakeven applied. _beRealized set to true.");
			}

			ForceRefresh(); // Refresh chart UI
		}
		
        #endregion
		
		#region Move Trail Stop 50%
		// Manually moves the active trailing stop closer to the current price by a percentage
		protected void MoveTrailingStopByPercentage(double percentage)
		{
		    Print($"{Time[0]}: MoveTrailingStopByPercentage button clicked. Percentage: {percentage:P1}"); // Log percentage

            // --- Pre-checks ---
		    if (isFlat)
		    {
		        Print($"{Time[0]}: MoveTrailingStopByPercentage: No active position.");
		        return;
		    }
            if (percentage <= 0 || percentage >= 1) // Percentage must be > 0 and < 1
            {
                 Print($"{Time[0]}: MoveTrailingStopByPercentage: Invalid percentage ({percentage:P1}). Must be between 0% and 100%.");
                 return;
            }
             // Ensure trailing is conceptually enabled
            if (!enableTrail && !enableFixedStopLoss)
            {
                 Print($"{Time[0]}: MoveTrailingStopByPercentage: Neither Trailing nor Fixed Stop enabled.");
                 return;
            }

            // --- Get Current State ---
		    double entryPrice = Position.AveragePrice;
            if (entryPrice == 0) { Print($"{Time[0]}: MoveTrailingStopByPercentage: Cannot adjust, entry price is 0."); return; } // Safety check
		    bool isLong = Position.MarketPosition == MarketPosition.Long;
		    double currentMarketPrice = Close[0];

            // --- Determine Current Stop Price ---
            // (Using the same robust logic as AdjustStopLoss)
            double currentStopPrice = 0;
            Order workingStop = Orders.FirstOrDefault(o => o.OrderState == OrderState.Working && o.IsStopMarket);

            if (workingStop != null)
            {
                currentStopPrice = workingStop.StopPrice;
                Print($"{Time[0]}: MoveTrailingStopByPercentage: Found working stop order {workingStop.OrderId} at price {currentStopPrice:F5}.");
            }
            else
            {
                Print($"{Time[0]}: MoveTrailingStopByPercentage: No working stop order found. Inferring current stop level...");
                if (enableFixedStopLoss) { // If fixed stop is primary, adjust that fixed level conceptually
                    currentStopPrice = isLong ? entryPrice - (InitialStop * TickSize) : entryPrice + (InitialStop * TickSize);
                    Print($"{Time[0]}: MoveTrailingStopByPercentage: Inferred from Fixed Stop setting. Current Stop Price: {currentStopPrice:F5}");
                } else if (enableTrail) { // If trailing is primary, infer from current trail calculation
                    double currentTrailTicks = CalculateTrailingStopTicks();
                    currentStopPrice = isLong ? currentMarketPrice - (currentTrailTicks * TickSize) : currentMarketPrice + (currentTrailTicks * TickSize);
                    Print($"{Time[0]}: MoveTrailingStopByPercentage: Inferred from Trailing Stop setting ({currentTrailTicks} ticks). Current Stop Price: {currentStopPrice:F5}");
                } else {
                     Print($"{Time[0]}: MoveTrailingStopByPercentage: Cannot determine current stop price level. Aborting.");
                     return;
                }
            }
			
            if (currentStopPrice == 0) { Print($"{Time[0]}: MoveTrailingStopByPercentage: Could not determine a valid current stop price. Aborting."); return; }

            // --- Calculate New Target Stop Price ---
            double distance = Math.Abs(currentMarketPrice - currentStopPrice);
            double moveAmount = distance * percentage;
            double newTargetStopPrice = isLong
                ? currentStopPrice + moveAmount // Move towards market
                : currentStopPrice - moveAmount; // Move towards market

            Print($"{Time[0]}: MoveTrailingStopByPercentage: Current Stop: {currentStopPrice:F5}, Market: {currentMarketPrice:F5}, Distance: {distance:F5}");
            Print($"{Time[0]}: MoveTrailingStopByPercentage: Move Amount: {moveAmount:F5} ({percentage:P1}), New Target Stop: {newTargetStopPrice:F5}");

            // --- Validate New Stop Price ---
            // Prevent moving stop TO or BEYOND the current market price
            if ((isLong && newTargetStopPrice >= currentMarketPrice) || (!isLong && newTargetStopPrice <= currentMarketPrice))
            {
                Print($"{Time[0]}: MoveTrailingStopByPercentage: Cannot move stop. New target price {newTargetStopPrice:F5} invalid relative to current market price {currentMarketPrice:F5}.");
                return; // Do not proceed
            }

            // --- Calculate Tick Offset for SetTrailStop (from Entry Price) ---
            double breakevenTicks = isLong
		        ? (entryPrice - newTargetStopPrice) / TickSize
		        : (newTargetStopPrice - entryPrice) / TickSize;

            Print($"{Time[0]}: MoveTrailingStopByPercentage: Calculated Tick Offset From Entry: {breakevenTicks:F2}");

            // --- Sanity Check Offset ---
            if (breakevenTicks <= 0)
            {
                 Print($"{Time[0]}: MoveTrailingStopByPercentage: Calculated non-positive breakevenTicks ({breakevenTicks:F2}). Aborting adjustment. Check logic or price validation.");
                 return;
            }

            // --- Apply to Relevant Labels using Safe Helper ---
		    List<string> orderLabels = GetRelevantOrderLabels();
            if (orderLabels.Count == 0)
            {
                 Print($"{Time[0]}: MoveTrailingStopByPercentage: No relevant order labels found to apply adjustment to.");
                 return;
            }

            Print($"{Time[0]}: MoveTrailingStopByPercentage: Applying adjustment (Offset: {breakevenTicks:F2} ticks from entry) to labels: {string.Join(", ", orderLabels)}");
		    foreach (string label in orderLabels)
		    {
		        SetTrailingStop(label, CalculationMode.Ticks, breakevenTicks, true);
		    }

			ForceRefresh(); // Refresh chart UI
		}

        #endregion // End Stop Adjustment region
		
		#region Button Definitions

		private List<ButtonDefinition> buttonDefinitions;

		private class ButtonDefinition
		{
			public string Name { get; set; }
			public string Content { get; set; }
			public string ToolTip { get; set; }
			public Action<KCAlgoBase, System.Windows.Controls.Button> InitialDecoration { get; set; }
			public Action<KCAlgoBase> ClickAction { get; set; } // Action to perform when clicked
		}

		private void InitializeButtonDefinitions()
		{
			buttonDefinitions = new List<ButtonDefinition>
			{
				new ButtonDefinition
				{
				    Name = AutoButton,
				    Content = "\uD83D\uDD12 Auto On",
				    ToolTip = "Enable (Green) / Disbled (Red) Auto Button",
				    InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off"),
				    ClickAction = (strategy) =>
				    {
				        strategy.isAutoEnabled = !strategy.isAutoEnabled;
				        strategy.isManualEnabled = !strategy.isManualEnabled;
				        strategy.autoDisabledByChop = false; // User took control, clear the system flag
				        strategy.DecorateButton(strategy.autoBtn, strategy.isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off");
				        strategy.DecorateButton(strategy.manualBtn, strategy.isManualEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off");
				        strategy.Print("Auto Button Clicked. Auto: " + strategy.isAutoEnabled);
				    }
				},
				new ButtonDefinition
				{
				    Name = ManualButton,
				    Content = "\uD83D\uDD12 Manual On",
				    ToolTip = "Enable (Green) / Disbled (Red) Manual Button",
				    InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isManualEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off"),
				    ClickAction = (strategy) =>
				    {
				        strategy.isManualEnabled = !strategy.isManualEnabled;
				        strategy.isAutoEnabled = !strategy.isAutoEnabled;
				        strategy.autoDisabledByChop = false; // User took control, clear the system flag
				        strategy.DecorateButton(strategy.manualBtn, strategy.isManualEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off");
				        strategy.DecorateButton(strategy.autoBtn, strategy.isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off");
				        strategy.Print("Manual Button Clicked. Manual: " + strategy.isManualEnabled);
				    }
				},				
				new ButtonDefinition
				{
					Name = LongButton,
					Content = "LONG",
					ToolTip = "Enable (Green) / Disbled (Red) Auto Long Entry",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isLongEnabled ? ButtonState.Enabled : ButtonState.Disabled, "LONG", "LONG Off"),
					ClickAction = (strategy) =>
					{
						strategy.isLongEnabled = !strategy.isLongEnabled;
						strategy.DecorateButton(strategy.longBtn, strategy.isLongEnabled ? ButtonState.Enabled : ButtonState.Disabled, "LONG", "LONG Off");
						strategy.Print("Long Enabled " + strategy.isLongEnabled);
					}
				},
				new ButtonDefinition
				{
					Name = ShortButton,
					Content = "SHORT",
					ToolTip = "Enable (Green) / Disbled (Red) Auto Short Entry",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isShortEnabled ? ButtonState.Enabled : ButtonState.Disabled, "SHORT", "SHORT Off"),
					ClickAction = (strategy) =>
					{
						strategy.isShortEnabled = !strategy.isShortEnabled;
						strategy.DecorateButton(strategy.shortBtn, strategy.isShortEnabled ? ButtonState.Enabled : ButtonState.Disabled, "SHORT", "SHORT Off");
						strategy.Print("Short Activated " + strategy.isShortEnabled);
					}
				},
				new ButtonDefinition
				{
					Name = QuickLongButton,
					Content = "Buy",
					ToolTip = "Quick Long Entry",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Buy", foreground: Brushes.White, background: Brushes.DarkGreen),
					ClickAction = (strategy) =>
					{
                        string primaryLabel = QLE;

						if (isManualEnabled /* Removed && strategy.uptrend - Allow manual override? */) // Consider if trend check is needed for manual
						{
							strategy.Print("Quick Long Button Clicked");
							strategy.quickLongBtnActive = true;

                            // --- 1. Submit Base Quick Entry ---
                            Order baseOrder = strategy.SubmitEntryOrder(primaryLabel, strategy.OrderType, Convert.ToInt32(strategy.Contracts));
                            if (baseOrder == null)
                            {
                                Print($"{Time[0]}: Failed to submit base quick long entry order {primaryLabel}. Aborting quick entry.");
                                strategy.quickLongBtnActive = false;
                                return;
                            }
                            PrintOnce($"Entry_Quick_Base_Submit_{primaryLabel}_{CurrentBar}", $"{Time[0]}: Submitted quick long entry: {primaryLabel}");

                            // --- 2. Submit Scale-In Orders ---
							strategy.EnterMultipleLongContracts(true);

                            // --- 3 & 4. Stop/Target Placement REMOVED - Handled by OnExecutionUpdate ---
                            // strategy.SetStopLosses(primaryLabel); // <<< REMOVED
                            // strategy.SetProfitTargets();         // <<< REMOVED
						}
                        else { /* ... */ }
					}
				},
				new ButtonDefinition
				{
					Name = QuickShortButton,
					Content = "Sell",
					ToolTip = "Quick Short Entry",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Sell", foreground: Brushes.White, background: Brushes.DarkRed),
					ClickAction = (strategy) =>
					{
                        string primaryLabel = QSE;

						if (isManualEnabled /* Removed && strategy.downtrend */ ) // Consider if trend check is needed for manual
						{
							strategy.Print("Quick Short Button Clicked");
							strategy.quickShortBtnActive = true;

                            // --- 1. Submit Base Quick Entry ---
                            Order baseOrder = strategy.SubmitEntryOrder(primaryLabel, strategy.OrderType, Convert.ToInt32(strategy.Contracts));
                            if (baseOrder == null)
                            {
                                Print($"{Time[0]}: Failed to submit base quick short entry order {primaryLabel}. Aborting quick entry.");
                                strategy.quickShortBtnActive = false;
                                return;
                            }
                             PrintOnce($"Entry_Quick_Base_Submit_{primaryLabel}_{CurrentBar}", $"{Time[0]}: Submitted quick short entry: {primaryLabel}");

                            // --- 2. Submit Scale-In Orders ---
							strategy.EnterMultipleShortContracts(true);

                            // --- 3 & 4. Stop/Target Placement REMOVED - Handled by OnExecutionUpdate ---
                            // strategy.SetStopLosses(primaryLabel); // <<< REMOVED
                            // strategy.SetProfitTargets();         // <<< REMOVED
						}
                        else { /* ... */ }
					}
				},
				new ButtonDefinition
				{
					Name = Add1Button,
					Content = "Add 1",
					ToolTip = "Add 1 contract to open position",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Add 1", foreground: Brushes.White, background: Brushes.DarkGreen),
					ClickAction = (strategy) => strategy.add1Entry()
				},
				new ButtonDefinition
				{
					Name = Close1Button,
					Content = "Close 1",
					ToolTip = "Close 1 contract from open position",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Close 1", foreground: Brushes.White, background: Brushes.DarkRed),
					ClickAction = (strategy) => strategy.close1Exit()
				},
				new ButtonDefinition
				{
					Name = BEButton,
					Content = "\uD83D\uDD12 BE On",
					ToolTip = "Enable (Green) / Disbled (Red) Auto BE",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.beSetAuto ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 BE On", "\uD83D\uDD13 BE Off"),
					ClickAction = (strategy) =>
					{
						strategy.beSetAuto = !strategy.beSetAuto;
						strategy.DecorateButton(strategy.BEBtn, strategy.beSetAuto ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 BE On", "\uD83D\uDD13 BE Off");
					}
				},
				new ButtonDefinition
				{
					Name = TSButton,
					Content = "\uD83D\uDD12 TS On",
					ToolTip = "Enable (Green) / Disbled (Red) Auto TS",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.enableTrail ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 TS On", "\uD83D\uDD13 TS Off"),
					ClickAction = (strategy) =>
					{
						strategy.enableTrail = !strategy.enableTrail;
						strategy.DecorateButton(strategy.TSBtn, strategy.enableTrail ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 TS On", "\uD83D\uDD13 TS Off");
					}
				},
				new ButtonDefinition
				{
					Name = MoveTSButton,
					Content = "Move TS",
					ToolTip = "Increase trailing stop",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Move TS", background: Brushes.DarkBlue, foreground: Brushes.Yellow),
					ClickAction = (strategy) =>
					{
						strategy.AdjustStopLoss(strategy.TickMove);
						strategy.ForceRefresh();
					}
				},
				new ButtonDefinition
				{
					Name = MoveTS50PctButton,
					Content = "Move TS 50%",
					ToolTip = "Move trailing stop 50% closer to the current price",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Move TS 50%", background: Brushes.DarkBlue, foreground: Brushes.Yellow),
					ClickAction = (strategy) =>
					{
						strategy.MoveTrailingStopByPercentage(0.5);
						strategy.ForceRefresh();
					}
				},
				new ButtonDefinition
				{
					Name = MoveToBeButton,
					Content = "Breakeven",
					ToolTip = "Move stop to breakeven if in profit",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Breakeven", background: Brushes.DarkBlue, foreground: Brushes.White),
					ClickAction = (strategy) =>
					{
						strategy.MoveToBreakeven();
						strategy.ForceRefresh();
					}
				},
				new ButtonDefinition
				{
					Name = CloseButton,
					Content = "Close All Positions",
					ToolTip = "Manual Close: CloseAllPosiions manually. Alert!!! Only works with the entries made by the strategy. Manual entries will not be closed from this option.",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Close All Positions", background: Brushes.DarkRed, foreground: Brushes.White),
					ClickAction = (strategy) =>
					{
						strategy.CloseAllPositions();
						strategy.ForceRefresh();
					}
				},
				new ButtonDefinition
				{
					Name = PanicButton,
					Content = "\u2620 Panic Shutdown",
					ToolTip = "PanicBtn: CloseAllPosiions",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "\u2620 Panic Shutdown", background: Brushes.DarkRed, foreground: Brushes.Yellow),
					ClickAction = (strategy) =>
					{
						strategy.FlattenAllPositions();
						strategy.ForceRefresh();
					}
				},
				new ButtonDefinition
				{
					Name = DonatePayPalButton,
					Content = "Donate (PayPal)",
					ToolTip = "Support the developer via PayPal",
					InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Donate (PayPal)", background: Brushes.Blue, foreground: Brushes.Yellow),
					ClickAction = (strategy) =>
					{
						strategy.HandlePayPalDonationClick();
					}
				}
			};
		}

		#endregion

		#region Button Decorations

		private enum ButtonState
		{
			Enabled,
			Disabled,
			Neutral
		}

		private void DecorateButton(System.Windows.Controls.Button button, ButtonState state, string contentOn, string contentOff = null, Brush foreground = null, Brush background = null)
		{
			switch (state)
			{
				case ButtonState.Enabled:
					button.Content = contentOn;
					button.Background = background ?? Brushes.DarkGreen;
					button.Foreground = foreground ?? Brushes.White;
					break;
				case ButtonState.Disabled:
					button.Content = contentOff ?? contentOn;
					button.Background = background ?? Brushes.DarkRed;
					button.Foreground = foreground ?? Brushes.White;
					break;
				case ButtonState.Neutral:
					button.Content = contentOn;
					button.Background = background ?? Brushes.LightGray;
					button.Foreground = foreground ?? Brushes.Black;
					break;
			}

			button.BorderBrush = Brushes.Black;
		}

		#endregion		
		
		#region Create WPF Controls
		protected void CreateWPFControls()
		{
			//	ChartWindow
			chartWindow	= System.Windows.Window.GetWindow(ChartControl.Parent) as Gui.Chart.Chart;
			
			// if not added to a chart, do nothing
			if (chartWindow == null)
				return;

			// this is the entire chart trader area grid
			chartTraderGrid			= (chartWindow.FindFirst("ChartWindowChartTraderControl") as Gui.Chart.ChartTrader).Content as System.Windows.Controls.Grid;
			
			// this grid contains the existing chart trader buttons
			chartTraderButtonsGrid	= chartTraderGrid.Children[0] as System.Windows.Controls.Grid;
			
			InitializeButtonDefinitions(); // Initialize the button definitions

			CreateButtons();

			// this grid is to organize stuff below
			lowerButtonsGrid = new System.Windows.Controls.Grid();
			
			// Initialize
    		InitializeButtonGrid();

			addedRow	= new System.Windows.Controls.RowDefinition() { Height = new GridLength(250) };
			
    		// SetButtons
    		SetButtonLocations();

    		// AddButtons
    		AddButtonsToPanel();			
				
			if (TabSelected())
				InsertWPFControls();

			chartWindow.MainTabControl.SelectionChanged += TabChangedHandler;

		}
		#endregion
		
		#region Create Buttons
		protected void CreateButtons()
		{						
			// this style (provided by NinjaTrader_MichaelM) gives the correct default minwidth (and colors) to make buttons appear like chart trader buttons
			Style basicButtonStyle	= System.Windows.Application.Current.FindResource("BasicEntryButton") as Style;			
	
			manualBtn = CreateButton(ManualButton, basicButtonStyle);
			autoBtn = CreateButton(AutoButton, basicButtonStyle);
			longBtn = CreateButton(LongButton, basicButtonStyle);
			shortBtn = CreateButton(ShortButton, basicButtonStyle);
			quickLongBtn = CreateButton(QuickLongButton, basicButtonStyle);
			quickShortBtn = CreateButton(QuickShortButton, basicButtonStyle);
			BEBtn = CreateButton(BEButton, basicButtonStyle);
			TSBtn = CreateButton(TSButton, basicButtonStyle);
			moveTSBtn = CreateButton(MoveTSButton, basicButtonStyle);
			moveTS50PctBtn = CreateButton(MoveTS50PctButton, basicButtonStyle);
			moveToBEBtn = CreateButton(MoveToBeButton, basicButtonStyle);
			add1Btn = CreateButton(Add1Button, basicButtonStyle);
			close1Btn = CreateButton(Close1Button, basicButtonStyle);
			closeBtn = CreateButton(CloseButton, basicButtonStyle);
			panicBtn = CreateButton(PanicButton, basicButtonStyle);
			donatePayPalBtn = CreateButton(DonatePayPalButton, basicButtonStyle);
		}

		private System.Windows.Controls.Button CreateButton(string buttonName, Style basicButtonStyle)
		{
			var definition = buttonDefinitions.FirstOrDefault(b => b.Name == buttonName);
			if (definition == null)
			{
				Print($"Error: Button definition not found for {buttonName}");
				return null; // Or throw an exception
			}

			var button = new System.Windows.Controls.Button
			{
				Name = buttonName,
				Height = 25,
				Margin = new Thickness(1, 0, 1, 0),
				Padding = new Thickness(0, 0, 0, 0),
				Style = basicButtonStyle,
				BorderThickness = new Thickness(1.5),
				IsEnabled = true,
				ToolTip = definition.ToolTip,
			};

			definition.InitialDecoration?.Invoke(this, button); // Apply initial decoration
			button.Click += OnButtonClick; // All buttons use the same click handler

			return button;
		}
		
		protected void InitializeButtonGrid()
		{
    		// Create new grid
    		lowerButtonsGrid = new System.Windows.Controls.Grid();

    		// Columns number
    		for (int i = 0; i < 2; i++)
    		{
        		lowerButtonsGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
    		}

    		// Row number
    		for (int i = 0; i <= 10; i++)
    		{
        		lowerButtonsGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
    		}
		}				

		protected void SetButtonLocations()
		{
			// Btn, Column, Row, Column span
			
    		SetButtonLocation(manualBtn, 0, 1);    // Column 0 2 pos
    		SetButtonLocation(autoBtn, 1, 1);    			
    		SetButtonLocation(longBtn, 0, 2);
    		SetButtonLocation(shortBtn, 1, 2);
   			SetButtonLocation(quickLongBtn, 0, 3);
    		SetButtonLocation(quickShortBtn, 1, 3);    	
   			SetButtonLocation(add1Btn, 0, 4);
    		SetButtonLocation(close1Btn, 1, 4);    		
   			SetButtonLocation(BEBtn, 0, 5);
    		SetButtonLocation(TSBtn, 1, 5); 
		    SetButtonLocation(moveTSBtn, 0, 6);
    		SetButtonLocation(moveTS50PctBtn, 1, 6);  
   			SetButtonLocation(moveToBEBtn, 0, 7, 2);
			SetButtonLocation(closeBtn, 0, 8, 2);
			SetButtonLocation(panicBtn, 0, 9, 2);			
			SetButtonLocation(donatePayPalBtn, 0, 10, 2);
		}		
		
		protected void SetButtonLocation(System.Windows.Controls.Button button, int column, int row, int columnSpan = 1)
		{
    		System.Windows.Controls.Grid.SetColumn(button, column);
    		System.Windows.Controls.Grid.SetRow(button, row);
    
   			if (columnSpan > 1)
        		System.Windows.Controls.Grid.SetColumnSpan(button, columnSpan);
		}		
		
		protected void AddButtonsToPanel()
		{
    		// Add Buttons to grid
    		lowerButtonsGrid.Children.Add(manualBtn);
    		lowerButtonsGrid.Children.Add(autoBtn);
    		lowerButtonsGrid.Children.Add(longBtn);
    		lowerButtonsGrid.Children.Add(shortBtn);
    		lowerButtonsGrid.Children.Add(quickLongBtn);
    		lowerButtonsGrid.Children.Add(quickShortBtn);
    		lowerButtonsGrid.Children.Add(add1Btn);
    		lowerButtonsGrid.Children.Add(close1Btn);
    		lowerButtonsGrid.Children.Add(BEBtn);
    		lowerButtonsGrid.Children.Add(TSBtn);  
    		lowerButtonsGrid.Children.Add(moveTSBtn);  
    		lowerButtonsGrid.Children.Add(moveTS50PctBtn);  
    		lowerButtonsGrid.Children.Add(moveToBEBtn);    
			lowerButtonsGrid.Children.Add(closeBtn);
			lowerButtonsGrid.Children.Add(panicBtn);			
			lowerButtonsGrid.Children.Add(donatePayPalBtn);
		}			
		#endregion
		
		#region Buttons Click Events
		
		protected void OnButtonClick(object sender, RoutedEventArgs rea)
		{
			Button button = sender as Button;

			var definition = buttonDefinitions.FirstOrDefault(b => b.Name == button.Name);
			if (definition != null)
			{
				definition.ClickAction?.Invoke(this);
			}
			else
			{
				Print($"Error: No click action defined for button {button.Name}");
			}
		}
		
		#endregion
		       
		#region Dispose
		protected void DisposeWPFControls() 
		{
			if (chartWindow != null)
			chartWindow.MainTabControl.SelectionChanged -= TabChangedHandler;

			//Unsubscribe from all button click events
			UnsubscribeButtonClick(manualBtn);
			UnsubscribeButtonClick(autoBtn);
			UnsubscribeButtonClick(longBtn);
			UnsubscribeButtonClick(shortBtn);
			UnsubscribeButtonClick(quickLongBtn);
			UnsubscribeButtonClick(quickShortBtn);
			UnsubscribeButtonClick(add1Btn);
			UnsubscribeButtonClick(close1Btn);
			UnsubscribeButtonClick(BEBtn);
			UnsubscribeButtonClick(TSBtn);
			UnsubscribeButtonClick(moveTSBtn);
			UnsubscribeButtonClick(moveTS50PctBtn);
			UnsubscribeButtonClick(moveToBEBtn);
			UnsubscribeButtonClick(closeBtn);
			UnsubscribeButtonClick(panicBtn);
			UnsubscribeButtonClick(donatePayPalBtn);
	
			RemoveWPFControls();
		}

		private void UnsubscribeButtonClick(Button button)
		{
			if (button != null)
			{
				button.Click -= OnButtonClick;
			}
		}
		#endregion
		
		#region Insert WPF
		public void InsertWPFControls()
		{
			if (panelActive)
				return;
			
			// add a new row (addedRow) for our lowerButtonsGrid below the ask and bid prices and pnl display			
			chartTraderGrid.RowDefinitions.Add(addedRow);
			System.Windows.Controls.Grid.SetRow(lowerButtonsGrid, (chartTraderGrid.RowDefinitions.Count - 1));
			chartTraderGrid.Children.Add(lowerButtonsGrid);

			panelActive = true;
		}
		#endregion
		
		#region Remove WPF
		protected void RemoveWPFControls()
		{
			if (!panelActive)
				return;
			
			if (chartTraderButtonsGrid != null || lowerButtonsGrid != null)
			{
				chartTraderGrid.Children.Remove(lowerButtonsGrid);
				chartTraderGrid.RowDefinitions.Remove(addedRow);
			}

			panelActive = false;
		}
		#endregion
		
		#region TabSelcected 
		protected bool TabSelected()
		{
			bool tabSelected = false;

			// loop through each tab and see if the tab this indicator is added to is the selected item
			foreach (System.Windows.Controls.TabItem tab in chartWindow.MainTabControl.Items)
				if ((tab.Content as Gui.Chart.ChartTab).ChartControl == ChartControl && tab == chartWindow.MainTabControl.SelectedItem)
					tabSelected = true;

			return tabSelected;
		}
		#endregion
		
		#region TabHandler
		protected void TabChangedHandler(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count <= 0)
				return;

			tabItem = e.AddedItems[0] as System.Windows.Controls.TabItem;
			if (tabItem == null)
				return;

			chartTab = tabItem.Content as Gui.Chart.ChartTab;
			if (chartTab == null)
				return;

			if (TabSelected())
				InsertWPFControls();
			else
				RemoveWPFControls();
		}		
		#endregion	

		#region Close All Positions
		protected void CloseAllPositions()
		{
		//	Close actual position manually
        //	Check if there is an open position
			Print("Position Closing");
			
			if(isLong) 
			{
				// Create the order labels array based on whether additional contracts exist
		        string[] orderLabels = additionalContractExists ? new[] { LE, LE2, LE3, LE4, QLE, "QLE2", "QLE3", "QLE4" } : new[] { LE };
		
		        // Apply the initial trailing stop for all relevant orders
		        foreach (string label in orderLabels)
		        {
		            ExitLong("Manual Exit", label);
		        }
			}
			else if(isShort) 
			{
				// Create the order labels array based on whether additional contracts exist
		        string[] orderLabels = additionalContractExists ? new[] { SE, SE2, SE3, SE4, QSE, "QSE2", "QSE3", "QSE4" } : new[] { SE };
		
		        // Apply the initial trailing stop for all relevant orders
		        foreach (string label in orderLabels)
		        {
		            ExitShort("Manual Exit", label);
		        }
			}		
		}	
		
        protected void FlattenAllPositions()
        {	    
			System.Collections.ObjectModel.Collection<Cbi.Instrument> instrumentsToClose = new System.Collections.ObjectModel.Collection<Instrument>();        
			instrumentsToClose.Add(Position.Instrument);
			Position.Account.Flatten(instrumentsToClose);		
		}
		
		protected void HandlePayPalDonationClick()
		{
			Print("Donate (PayPal) button clicked."); // Log the click

		    // Check if the URL parameter has been set
		    if (string.IsNullOrWhiteSpace(paypal))
		    {
		        Print("PayPal Donation URL is not configured in strategy parameters.");
		        // Optionally show a message box to the user (requires adding `using System.Windows;`)
		        // MessageBox.Show("PayPal Donation URL is not configured in the strategy parameters.", "Missing URL", MessageBoxButton.OK, MessageBoxImage.Warning);
		        return; // Exit if no URL is set
		    }
		
		    try
		    {
		        // Use Process.Start to open the URL in the default browser
		        System.Diagnostics.Process.Start(paypal);
		        Print($"Attempting to open PayPal URL: {paypal}");
		    }
		    catch (Exception ex)
		    {
		        // Handle potential errors (e.g., invalid URL format, OS permissions)
		        Print($"Error opening PayPal URL '{paypal}': {ex.Message}");
		        // Optionally show a message box to the user
		        // MessageBox.Show($"Could not open the PayPal donation link.\nURL: {paypal}\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		    }
		}

		protected void add1Entry()
		{
		    int oneContract = 1;
		    Position openPosition = Position;
		    if (openPosition == null || openPosition.MarketPosition == MarketPosition.Flat)
		    {
		        Print($"{Time[0]}: Add1: No open position.");
		        return;
		    }
		
		    double currentPositionQty = openPosition.Quantity;
		    if (currentPositionQty + oneContract > EntriesPerDirection)
		    {
		         Print($"{Time[0]}: Add1: Cannot add contract, would exceed EntriesPerDirection limit ({EntriesPerDirection}).");
		         return;
		    }
		
		    try
		    {
		        if (isLong && uptrend) // Consider adding more checks if needed
		        {
		            string addLabel = quickLongBtnActive ? Add1LE : Add1LE; // Or maybe keep LE/QLE if truly intended? Decide based on desired stop/target behavior.
		            Print($"{Time[0]}: Adding 1 Long contract with label {addLabel}.");
		            // SubmitEntryOrder(addLabel, OrderType, oneContract); // Use the helper
		             EnterLong(oneContract, addLabel); // Or directly if SubmitEntryOrder is only for initial entries
		            // Q: Does this added contract need its own stop/target or share the main one?
		            // If sharing, no SetStop/SetTarget needed here. If separate, add calls here.
		        }
		        else if (isShort && downtrend) // Consider adding more checks
		        {
		             string addLabel = quickShortBtnActive ? Add1SE : Add1SE; // Or maybe SE/QSE?
		             Print($"{Time[0]}: Adding 1 Short contract with label {addLabel}.");
		             // SubmitEntryOrder(addLabel, OrderType, oneContract);
		              EnterShort(oneContract, addLabel);
		             // Add stop/target if needed for this specific contract
		        }
		        else
		        {
		             Print($"{Time[0]}: Add1: Cannot add contract. Position direction/trend mismatch (IsLong: {isLong}, Uptrend: {uptrend}, IsShort: {isShort}, Downtrend: {downtrend}).");
		        }
		    }
		    catch (Exception ex)
		    {
		        Print($"{Time[0]}: Failed to add contract due to: {ex.Message}");
		        orderErrorOccurred = true;
		    }
		}

		protected void close1Exit()
		{
			// Print("Close 1 button clicked."); // Logging handled in OnButtonClick
			int oneContract = 1;

        	if (Position.MarketPosition != MarketPosition.Flat) // Check if position exists
        	{
                if (Position.Quantity >= oneContract) // Check if there's at least one contract to close
                {
				    CloseOneContractFromPosition(); // Call the corrected method
                }
                else
                {
                    Print($"Cannot close {oneContract} contract. Position quantity ({Position.Quantity}) is less than requested.");
                }
			}
		}	
		
		protected void CloseOneContractFromPosition()
		{
		 	int contractsToClose = 1;
		    try
		    {
                // Check position state again for safety just before submitting
				if (Position.MarketPosition == MarketPosition.Long && Position.Quantity >= contractsToClose)
                {
                    Print($"{Time[0]}: Submitting request to close {contractsToClose} long contract(s) (FIFO).");
                    // Exit 1 long contract using FIFO logic by providing quantity and NO specific fromEntrySignal
                    ExitLong(contractsToClose, ManualClose1, ""); // "" or null for fromEntrySignal uses FIFO
                }
                else if (Position.MarketPosition == MarketPosition.Short && Position.Quantity >= contractsToClose)
                {
                     Print($"{Time[0]}: Submitting request to close {contractsToClose} short contract(s) (FIFO).");
                    // Exit 1 short contract using FIFO logic
                    ExitShort(contractsToClose, ManualClose1, ""); // "" or null for fromEntrySignal uses FIFO
                }
                else if (Position.Quantity < contractsToClose)
                {
                     Print($"{Time[0]}: Cannot close {contractsToClose} contract(s). Position quantity ({Position.Quantity}) is less than requested.");
                }
                else // Position is Flat or Direction unknown
                {
                    Print($"{Time[0]}: No position exists or direction unknown. Cannot close {contractsToClose} contract(s).");
                }
		    }
		    catch (Exception ex)
		    {
		        Print($"{Time[0]}: Failed to submit request to close {contractsToClose} contract(s) due to: {ex.Message}");
                orderErrorOccurred = true; // Flag error
		    }
		}
		
		protected void AddContractToOpenPosition()
		{   // Add 1
			int oneContract = 1;
		    try
		    {
				if(isLong && uptrend) {
					if (!quickLongBtnActive)
					{	
						EnterLong(oneContract, @LE);	
//						EnterMultipleLongContracts(false);
					}
					if (quickLongBtnActive)
					{	
						EnterLong(oneContract, @QLE);
//						EnterMultipleLongContracts(true);
						
					}					
				
				}else if(isShort && downtrend) {
					if (!quickShortBtnActive)
					{	
						EnterShort(oneContract, @SE);
//						EnterMultipleShortContracts(false);
						
					//	if(OrderType == OrderType.Market) EnterShort(oneContract, @SE);
					//	if(!OrderType == OrderType.Market) EnterShortLimit(oneContract, GetCurrentAsk(0), @SE);	
					}	
					if (quickShortBtnActive)
					{	
						EnterShort(oneContract, @QSE);
//						EnterMultipleShortContracts(true);
						
					//	if(OrderType == OrderType.Market) EnterShort(oneContract, @QSE);
					//	if(!OrderType == OrderType.Market) EnterShortLimit(oneContract, GetCurrentAsk(0), @QSE);
					}						
				}	
		        else {
		            Print("No open position to close contracts from.");
		        }
		    }
		    catch (Exception ex)
		    {
		        Print($"Failed to add contracts due to: {ex.Message}");
		    }
		}
		
		protected void checkPositions()
		{
		//	Detect unwanted Positions opened (possible rogue Order?)
	        double currentPosition = Position.Quantity; // Get current position quantity
		
			if (isFlat)
			{
		        foreach (var order in Orders)
		        {
		            if (order != null) CancelOrder(order);
		        }				
			}
		}	
		
		protected void checkOrder()
		{
		// Verify one active order and set myStopPrice and mylimitPrice to be used in changing orders when add or close 1 contracts to open positions
			activeOrder = false;
			
			if (Orders.Count != 0)
			{
				Print($"{Times[0][0].TimeOfDay} ACTIVE Orders Count:  {Orders.Count}");
				foreach (var order in Orders)
		        {
					string entrySignal = order.FromEntrySignal;
					Print($"{Times[0][0].TimeOfDay} myOrder NOT null {order.OrderId}  StopPrice:  {order.StopPrice}   LimitPrice  {order.LimitPrice}    orderQuantity {order.Quantity}   tiene el estado: {order.OrderState}  y es del tipo {order.OrderTypeString}    FROM EntrySignal {entrySignal}");
		            // Verificar el estado de cada orden
					if (order.OrderState == OrderState.Filled)
		            {
		                myEntryOrder = order;
						if (order.IsStopMarket && entrySignal != "Add 1")
						{
							myStopOrder = order;
							myStopPrice = myStopOrder.StopPrice;
						}	
						if (order.IsLimit &&  entrySignal != "Add 1") 
						{
							myLimitPrice = myEntryOrder.LimitPrice;
							
						}	
		            }					
					else if (order.OrderState == OrderState.TriggerPending && entrySignal != "Add 1")
		            {
		                if (order.IsStopMarket)
						{
							myStopOrder = order;
							myStopPrice = myStopOrder.StopPrice;
						}
		            }
					else if (order.OrderState == OrderState.Working && entrySignal != "Add 1")
		            {						
						if (order.IsLimit)
						{ 
							myTargetOrder = order;
							myLimitPrice = myTargetOrder.LimitPrice;	
						}	
		            }					
		            else
		            {
		                Print("La orden " + order.OrderId + " tiene el estado: " + order.OrderState);
		            }							
		        }
				Print($"{Times[0][0].TimeOfDay} myEntryOrder NOT null {myEntryOrder.OrderId}  StopPrice:  {myEntryOrder.StopPrice}   LimitPrice  {myEntryOrder.LimitPrice}    orderQuantity {myEntryOrder.Quantity}   tiene el estado: {myEntryOrder.OrderState}  y es del tipo {myEntryOrder.OrderTypeString}");
				activeOrder = true;
			}
		}
		
		protected bool checkTimers()
		{
		//	check we are in timer	
			if((Times[0][0].TimeOfDay >= Start.TimeOfDay) && (Times[0][0].TimeOfDay < End.TimeOfDay) 
					|| (Time2 && Times[0][0].TimeOfDay >= Start2.TimeOfDay && Times[0][0].TimeOfDay <= End2.TimeOfDay)
					|| (Time3 && Times[0][0].TimeOfDay >= Start3.TimeOfDay && Times[0][0].TimeOfDay <= End3.TimeOfDay)
					|| (Time4 && Times[0][0].TimeOfDay >= Start4.TimeOfDay && Times[0][0].TimeOfDay <= End4.TimeOfDay)
					|| (Time5 && Times[0][0].TimeOfDay >= Start5.TimeOfDay && Times[0][0].TimeOfDay <= End5.TimeOfDay)
					|| (Time6 && Times[0][0].TimeOfDay >= Start6.TimeOfDay && Times[0][0].TimeOfDay <= End6.TimeOfDay)
			)
			{
				return true;
			}
			else
			{
				return false;
			}			
		}
		
		protected string GetActiveTimer()
		{
		//	check active timer	
		    TimeSpan currentTime = Times[0][0].TimeOfDay;
		
		    if ((Times[0][0].TimeOfDay >= Start.TimeOfDay) && (Times[0][0].TimeOfDay < End.TimeOfDay))
		    {
		        return $"{Start:HH\\:mm} - {End:HH\\:mm}";
		    }
		    else if (Time2 && Times[0][0].TimeOfDay >= Start2.TimeOfDay && Times[0][0].TimeOfDay <= End2.TimeOfDay)
		    {
		        return $"{Start2:HH\\:mm} - {End2:HH\\:mm}";
		    }
		    else if (Time3 && Times[0][0].TimeOfDay >= Start3.TimeOfDay && Times[0][0].TimeOfDay <= End3.TimeOfDay)
		    {
		        return $"{Start3:HH\\:mm} - {End3:HH\\:mm}";
		    }
		    else if (Time4 && Times[0][0].TimeOfDay >= Start4.TimeOfDay && Times[0][0].TimeOfDay <= End4.TimeOfDay)
		    {
		        return $"{Start4:HH\\:mm} - {End4:HH\\:mm}";
		    }
		    else if (Time5 && Times[0][0].TimeOfDay >= Start5.TimeOfDay && Times[0][0].TimeOfDay <= End5.TimeOfDay)
		    {
		        return $"{Start5:HH\\:mm} - {End5:HH\\:mm}";
		    }
		    else if (Time6 && Times[0][0].TimeOfDay >= Start6.TimeOfDay && Times[0][0].TimeOfDay <= End6.TimeOfDay)
		    {
		        return $"{Start6:HH\\:mm} - {End6:HH\\:mm}";
		    }
		
		    return "No active timer";
		}
		
		#endregion				
		
		#region DrawPnl
		protected void ShowPNLStatus() {
			textLine0 = "Active Timer";
			textLine1 = GetActiveTimer();
			textLine2 = "Long Per Direction";
			textLine3 = $"{counterLong} / {longPerDirection} | " + (TradesPerDirection ? "On" : "Off");
			textLine4 = "Short Per Direction";
			textLine5 = $"{counterShort} / {shortPerDirection} | " + (TradesPerDirection ? "On" : "Off");
			textLine6 = "Bars Since Exit ";
			textLine7 = $"{iBarsSinceExit}    |    " + (iBarsSinceExit > 1 ?  "On" : "Off");
			string statusPnlText = textLine0 + "\t" + textLine1 + "\n" + textLine2 + "  " + textLine3 + "\n" + textLine4 + "  " + textLine5+ "\n" + textLine6 + "\t";
			SimpleFont font = new SimpleFont("Arial", 16);
			
			Draw.TextFixed(this, "statusPnl", statusPnlText, PositionPnl, colorPnl, font, Brushes.Transparent, Brushes.Transparent, 0);
								
		}
		#endregion			
		
		#region Discord Signal
		private async Task SendSignalToDiscordAsync(string direction, double entryPrice, double stopLoss, double profitTarget, DateTime entryTime)
		{
		    try
		    {
		        // Check rate limit
		        if (DateTime.Now - lastDiscordMessageTime < discordRateLimitInterval)
		        {
		            Print("Skipping Discord signal due to rate limit.");
		            return;
		        }
		
		        // Update the last sent time
		        lastDiscordMessageTime = DateTime.Now;
		
		        // Create the embed message for Discord
		        var fields = new List<object>
		        {
		            new { name = "Direction", value = direction, inline = true },
		            new { name = "Entry Price", value = entryPrice.ToString("F2"), inline = true },
		            new { name = "Stop Loss", value = stopLoss.ToString("F2"), inline = true },
		            new { name = "Profit Target", value = profitTarget.ToString("F2"), inline = true },
		            new { name = "Time", value = entryTime.ToString("HH:mm:ss"), inline = false }
		        };
		
		        var embed = new
		        {
		            title = $"Trade Signal: {direction}",
		            color = direction.Contains("LONG") ? 3066993 : 15158332, // Green for long, Red for short
		            fields = fields
		        };
		
		        using (var client = new HttpClient())
		        {
		            var payload = new { username = "Trading Bot", embeds = new[] { embed } };
		            var json = new JavaScriptSerializer().Serialize(payload);
		            var content = new StringContent(json, Encoding.UTF8, "application/json");
		
		            var webhookUrl = DiscordWebhooks;
		
		            var response = await client.PostAsync(webhookUrl, content);
		
		            if (response.IsSuccessStatusCode)
		            {
		                Print($"Discord Signal sent: {direction} - Time: {entryTime:HH:mm:ss}");
		            }
		            else
		            {
		                Print($"Discord Signal failed: {response.StatusCode} {response.ReasonPhrase}");
		            }
		        }
		    }
		    catch (Exception ex)
		    {
		        Print($"Error sending Discord Signal: {ex.Message}");
		    }
		}		
		#endregion		
		
		#region Entry Signals & Inits
		
		protected abstract bool ValidateEntryLong(); 
        	
		// protected abstract bool CheckLongEntryConditions();	
		
        protected abstract bool ValidateEntryShort();

		// protected abstract bool CheckShortEntryConditions();	
		
        protected virtual bool ValidateExitLong() {
			return false;
		}

        protected virtual bool ValidateExitShort() {
			return false;
		}
		
		protected abstract void InitializeIndicators();		
		
		protected virtual void addDataSeries() {}
		
		#endregion
		
		#region Daily PNL
		
		protected override void OnPositionUpdate(Cbi.Position position, double averagePrice, 
			int quantity, Cbi.MarketPosition marketPosition)
		{			
			if (isFlat && SystemPerformance.AllTrades.Count > 0)
			{
//				PositionPnl = TextPosition.BottomLeft;
//				totalPnL = 0; //backtest
			
				totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit; ///Double that sets the total PnL 
				dailyPnL = (totalPnL) - (cumPnL); ///Your daily limit is the difference between these
				
				// Re-enable the strategy if it was disabled by the DD and totalPnL increases
				if (enableTrailingDrawdown && trailingDrawdownReached && totalPnL > maxProfit - TrailingDrawdown)
	            {
	                trailingDrawdownReached = false;
					isAutoEnabled = true;
					Print("Trailing Drawdown Lifted. Strategy Re-Enabled!");
				}
	
				
				if (dailyPnL <= -DailyLossLimit) //Print this when daily Pnl is under Loss Limit
				{
					Print("Daily Loss of " + DailyLossLimit +  " has been hit. No More Entries! Daily PnL >> " + dailyPnL + " <<" +  Time[0]);
					
					Text myTextLoss = Draw.TextFixed(this, "loss_text", "Daily Loss of " + DailyLossLimit +  " has been hit. No More Entries! Daily PnL >> " + "$" + totalPnL + " <<", PositionDailyPNL, colorDailyProfitLoss, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 100);
					myTextLoss.Font = new SimpleFont("Arial", 18) {Bold = true };
				}				
				
				if (dailyPnL >= DailyProfitLimit) //Print this when daily Pnl is above Profit limit
				{
					
					Print("Daily Profit of " + DailyProfitLimit +  " has been hit. No more Entries! Daily PnL >>" +  dailyPnL + " <<" + Time[0]);
					
					Text myTextProfit = Draw.TextFixed(this, "profit_text", "Daily Profit of " + DailyProfitLimit +  " has been hit. No more Entries! Daily PnL >>" + "$" +  totalPnL + " <<", PositionDailyPNL, colorDailyProfitLoss, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 100);
					myTextProfit.Font = new SimpleFont("Arial", 18) {Bold = true };	
				}
			}	
			
			if (isFlat)	checkPositions(); // Detect unwanted Positions opened (possible rogue Order?)						
		}
		
		#endregion	
		
//		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
//		{
//			base.OnRender(chartControl, chartScale);
//			if (showDailyPnl) DrawStrategyPnL(chartControl);
//		}

		protected void DrawStrategyPnL()
		{	
			if (Account == null) return; // Added safety check for connection
		
		    // ... (Get account PnL logic remains the same) ...
		    double accountRealized = (State == State.Realtime) ? Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar) : SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
		    double accountUnrealized = (State == State.Realtime) ? Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar) : (Position != null && Position.MarketPosition != MarketPosition.Flat ? Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]) : 0); // Safety for Position
		    double accountTotal = accountRealized + accountUnrealized;
		    dailyPnL = accountTotal - cumPnL; // Assuming cumPnL is correctly managed elsewhere
		    if (accountTotal > maxProfit) maxProfit = accountTotal;
		
		    // --- Determine Trend and Signal Strings ---
		    // Need to ensure these conditions don't themselves cause issues if called too early
		    // But typically OnBarUpdate handles the main indicator calculations first.
		    string trendStatus = uptrend ? "Up" : downtrend ? "Down" : "Neutral";
		    string signalStatus = "No Signal"; // Default

		    // These checks rely on flags set in OnBarUpdate, which should be safe if OnBarUpdate handled warm-up
		    if (IsLongEntryConditionMet()) signalStatus = "Long Ready";
		    else if (IsShortEntryConditionMet()) signalStatus = "Short Ready";
		
		    // --- Apply Overrides based on State ---
		    if (!isFlat) signalStatus = "In Position";
			
			if (EnableChoppinessDetection)
			{
			    if (!isAutoEnabled && !autoDisabledByChop) signalStatus = "Auto OFF (Manual)"; // User turned it off
			
				if (marketIsChoppy) // Choppiness override (applies even if Auto is OFF manually)
			    {
			        trendStatus = "Choppy";
			        signalStatus = "No Trade (Chop)";
			    }
			     if (!isAutoEnabled && autoDisabledByChop) signalStatus = "Auto OFF (Chop)"; // System turned it off due to chop
			}
			
		    // Other overrides (higher priority?)
		    if (!checkTimers()) signalStatus = "Outside Hours";
		    if (orderErrorOccurred) signalStatus = "Order Error!";
		    if (enableTrailingDrawdown && trailingDrawdownReached) signalStatus = "Drawdown Limit Hit";
		    if (dailyLossProfit && dailyPnL <= -DailyLossLimit) signalStatus = "Loss Limit Hit";
		    if (dailyLossProfit && dailyPnL >= DailyProfitLimit) signalStatus = "Profit Limit Hit";
		
		    string pnlSource = (State == State.Realtime) ? "Account" : "System";
		    // Added null check for Account.Connection
		    string connectionStatus = (Account.Connection != null) ? Account.Connection.Status.ToString() : "N/A";
		
		    // --- FIXED INDICATOR VALUE DISPLAY ---
		    // Instead of IsValidDataPoint, check if CurrentBar is sufficient for the indicator's period.
		    // This prevents displaying default values (like 0) during the initial strategy warm-up.
		
		    // Get the period for Momentum1 (it was hardcoded 14 during initialization)
		    // If Momentum1 instance exists and has a Period property, use that, otherwise default to known value
		    int momentumPeriod = (Momentum1 != null ? Momentum1.Period : 14);
		    // BuySellPressure readiness check - using BarsRequiredToTrade as proxy
		    // Assuming BuySellPressure1 needs at least BarsRequiredToTrade bars.
		    int buySellPressureRequiredBars = BarsRequiredToTrade; // Or specific period if known for BuySellPressure
		
		    // Check CurrentBar against the required period (0-based index means CurrentBar >= Period - 1)
			
            string adxStatus = currentAdx > AdxThreshold ? $"Trending ({currentAdx:F1})" : $"Choppy ({currentAdx:F1})";
			string momoStatus = currentMomentum > 0 ? $"Up ({currentMomentum:F1})" : currentMomentum < 0 ? $"Down ({currentMomentum:F1})" : $"Neutral ({currentMomentum:F1})";
		    // Assuming buyPressure/sellPressure series are populated when BuySellPressure1 is calculated in OnBarUpdate
		    string buyPressText = CurrentBar >= buySellPressureRequiredBars - 1 ? buyPressure[0].ToString("F1") : "N/A";
		    string sellPressText = CurrentBar >= buySellPressureRequiredBars - 1 ? sellPressure[0].ToString("F1") : "N/A";
		    // --- END FIXED INDICATOR VALUE DISPLAY ---

		
		    string realTimeTradeText =
		        $"{Account.Name} | {(Account.Connection != null ? Account.Connection.Options.Name : "N/A")} ({connectionStatus})\n" +
		        $"PnL Src: {pnlSource}\n" +
		        $"Real PnL:\t{accountRealized:C}\n" +
		        $"Unreal PnL:\t{accountUnrealized:C}\n" +
		        $"Total PnL:\t{accountTotal:C}\n" +
		        $"Daily PnL:\t{dailyPnL:C}\n" +
		        $"Max Profit:\t{(maxProfit == double.MinValue ? "N/A" : maxProfit.ToString("C"))}\n" +
		        $"-------------\n" +
		        $"ADX:\t\t{adxStatus}\n" +             // Use safe text
		        $"Momentum:\t{momoStatus}\n" +           // Use safe text
		        $"Buy Pressure:\t{buyPressText}\n" +   // Use safe text
		        $"Sell Pressure:\t{sellPressText}\n" +  // Use safe text
				$"ATR:\t\t{currentAtr:F2}\n" +
		        $"-------------\n" +
		        $"Trend:\t{trendStatus}\n" +      // Use overridden status
		        $"Signal:\t{signalStatus}";       // Use overridden status

		    SimpleFont font = new SimpleFont("Arial", 16);
		    Brush pnlColor = accountTotal == 0 ? Brushes.Cyan : accountTotal > 0 ? Brushes.Lime : Brushes.Pink;
		
		    try
		    {
		        // Ensure ChartControl and other UI elements are available before drawing
		        //if (chartControl != null)
		        //{
		        Draw.TextFixed(this, "realTimeTradeText", realTimeTradeText, PositionDailyPNL, pnlColor, font, Brushes.Transparent, Brushes.Transparent, 0);
		        //}
		    }
		    catch (Exception ex) { Print($"Error drawing PNL display: {ex.Message}"); }
		}
		
		#region KillSwitch
		protected void KillSwitch()
		{
		    totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
		    dailyPnL = totalPnL + Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
		
		    // Track the Highest Profit Achieved
		    if (totalPnL > maxProfit)
		    {
		        maxProfit = totalPnL;
		    }
		
		    // Determine all relevant order labels
		    List<string> longOrderLabels = new List<string> { LE, QLE }; // Base Labels for Longs
		    List<string> shortOrderLabels = new List<string> { SE, QSE }; // Base Labels for Shorts
		
		    if (EnableProfitTarget2)
		    {
		        longOrderLabels.AddRange(new[] { LE2, "QLE2" });
		        shortOrderLabels.AddRange(new[] { SE2, "QSE2" });
		    }

		    if (EnableProfitTarget3)
		    {
		        longOrderLabels.AddRange(new[] { LE3, "QLE3" });
		        shortOrderLabels.AddRange(new[] { SE3, "QSE3" });
		    }
		
		    if (EnableProfitTarget4)
		    {
		        longOrderLabels.AddRange(new[] { LE4, "QLE4" });
		        shortOrderLabels.AddRange(new[] { SE4, "QSE4" });
		    }
		
		    // Common Action: Close all Positions and Disable the Strategy
		    Action closeAllPositionsAndDisableStrategy = () =>
		    {
		        foreach (string label in longOrderLabels)
		        {
		            ExitLong(Convert.ToInt32(Position.Quantity), @"LongExitKillSwitch", label);
		        }
		
		        foreach (string label in shortOrderLabels)
		        {
		            ExitShort(Convert.ToInt32(Position.Quantity), @"ShortExitKillSwitch", label);
		        }
		
		        isAutoEnabled = false;
		        Print("Kill Switch Activated: Strategy Disabled!");
		    };

		    if (dailyLossProfit && enableTrailingDrawdown) //Check both the enableDailyLossLimit and enableTrailingDrawdown
		    {
		        if (totalPnL >= StartTrailingDD && (maxProfit - totalPnL) >= TrailingDrawdown)
		        {
		            closeAllPositionsAndDisableStrategy();
		            trailingDrawdownReached = true;
		        }
		    }
		
		    if (dailyPnL <= -DailyLossLimit)
		    {
		        closeAllPositionsAndDisableStrategy();
		    }
		
		    if (dailyPnL >= DailyProfitLimit)
		    {
		        closeAllPositionsAndDisableStrategy();
		    }
		
			if (!isAutoEnabled)
				Print("Kill Switch Activated!!!");
		}
		#endregion
		
		#region Custom Property Manipulation	
		
		public void ModifyProperties(PropertyDescriptorCollection col)
        {
			if (TradesPerDirection == false)
            {
				col.Remove(col.Find("longPerDirection", true));
				col.Remove(col.Find("shortPerDirection", true));
            }
			if (Time2 == false)
            {
				col.Remove(col.Find("Start2", true));
				col.Remove(col.Find("End2", true));
            }
			if (Time3 == false)
            {
				col.Remove(col.Find("Start3", true));
				col.Remove(col.Find("End3", true));
            }
			if (Time4 == false)
            {
				col.Remove(col.Find("Start4", true));
				col.Remove(col.Find("End4", true));
            }
			if (Time5 == false)
            {
				col.Remove(col.Find("Start5", true));
				col.Remove(col.Find("End5", true));
            }
			if (Time6 == false)
            {
				col.Remove(col.Find("Start6", true));
				col.Remove(col.Find("End6", true));
            }
		}
		
		public void ModifyBESetAutoProperties(PropertyDescriptorCollection col) {
			if (showctrlBESetAuto == false) {
				col.Remove(col.Find("BE_Trigger", true));
				col.Remove(col.Find("BE_Offset", true));
			}
		}		

		// This method now controls visibility of mode-SPECIFIC parameters
		public void ModifyProfitTargetProperties(PropertyDescriptorCollection col)
        {
            // Default visibility: Assume Fixed mode is default if none are explicitly true (though setters prevent this)
            bool fixedModeActive = enableFixedProfitTarget;
            bool dynamicModeActive = enableDynamicProfitTarget;
            bool regChanModeActive = enableRegChanProfitTarget;

            // If Fixed Profit is NOT active, remove its parameters
            if (!fixedModeActive)
            {
                col.Remove(col.Find("EnableProfitTarget2", true));
                col.Remove(col.Find("Contracts2", true));
                col.Remove(col.Find("ProfitTarget2", true));
                col.Remove(col.Find("EnableProfitTarget3", true));
                col.Remove(col.Find("Contracts3", true));
                col.Remove(col.Find("ProfitTarget3", true));
                col.Remove(col.Find("EnableProfitTarget4", true));
                col.Remove(col.Find("Contracts4", true));
                col.Remove(col.Find("ProfitTarget4", true));
            }

            // Remove Dynamic Profit specific parameters if not active (none currently)
            // if (!dynamicModeActive) { ... remove dynamic params ... }

            // Remove RegChan Profit specific parameters if not active
            if (!regChanModeActive)
            {
                 col.Remove(col.Find("MinRegChanTargetDistanceTicks", true));
            }

            // Remove base ProfitTarget if Dynamic or RegChan mode is active
            if (dynamicModeActive || regChanModeActive)
            {
                col.Remove(col.Find("ProfitTarget", true));
            }
		}
		
		public void ModifyTrailProperties(PropertyDescriptorCollection col) {
			if (showTrailOptions == false) {
				col.Remove(col.Find("TrailSetAuto", true));
				col.Remove(col.Find("AtrPeriod", true));
				col.Remove(col.Find("atrMultiplier", true));
				col.Remove(col.Find("RiskRewardRatio", true));
				col.Remove(col.Find("Trail_Frequency", true));
				col.Remove(col.Find("TrailByThreeStep", true));
				col.Remove(col.Find("threeStepTrail", true));
				col.Remove(col.Find("step1ProfitTrigger", true));
				col.Remove(col.Find("step2ProfitTrigger", true));
				col.Remove(col.Find("step3ProfitTrigger", true));
				col.Remove(col.Find("step1StopLoss", true));
				col.Remove(col.Find("step2StopLoss", true));
				col.Remove(col.Find("step3StopLoss", true));
				col.Remove(col.Find("step1Frequency", true));
				col.Remove(col.Find("step2Frequency", true));
				col.Remove(col.Find("step3Frequency", true));				
			}
		}	
		
		public void ModifyTrailStopTypeProperties(PropertyDescriptorCollection col)
        {
			// Hide/Show ATR Trail parameters
			if (trailStopType != TrailStopTypeKC.ATR_Trail) {
				// col.Remove(col.Find("TrailSetAuto", true)); // TrailSetAuto might be confusing, remove if ATR specific
				col.Remove(col.Find("AtrPeriod", true));      // This is likely used elsewhere, be careful removing
				col.Remove(col.Find("atrMultiplier", true));
				// col.Remove(col.Find("RiskRewardRatio", true)); // Used by ATR PT, not trail? Keep separate logic for PT params
				// col.Remove(col.Find("Trail_Frequency", true)); // Seems obsolete
			}

            // Hide/Show 3-Step Trail parameters
			if (trailStopType != TrailStopTypeKC.Three_Step_Trail) {
				// col.Remove(col.Find("threeStepTrail", true)); // This is the bool itself, keep visible? Or remove? Let's remove specific params.
				col.Remove(col.Find("step1ProfitTrigger", true));
				col.Remove(col.Find("step2ProfitTrigger", true));
				col.Remove(col.Find("step3ProfitTrigger", true));
				col.Remove(col.Find("step1StopLoss", true));
				col.Remove(col.Find("step2StopLoss", true));
				col.Remove(col.Find("step3StopLoss", true));
				// col.Remove(col.Find("step1Frequency", true)); // Obsolete?
				// col.Remove(col.Find("step2Frequency", true));
				// col.Remove(col.Find("step3Frequency", true));
			}

            // Hide/Show Regression Channel Trail parameters
            if (trailStopType != TrailStopTypeKC.Regression_Channel_Trail) // <-- Check for the specific enum value
            {
                 col.Remove(col.Find("MinRegChanStopDistanceTicks", true)); // <-- Remove if not RegChan Trail
            }

            // Note: Tick_Trail and Fixed_Stop don't have specific parameters controlled *here*
            // Their behavior is controlled by the 'enableTrail' and 'enableFixedStopLoss' flags set by the property setter.
		}
		
		public void ModifyTrailSetAutoProperties(PropertyDescriptorCollection col) {
			if (showAtrTrailSetAuto == false) {
				col.Remove(col.Find("AtrPeriod", true));
				col.Remove(col.Find("atrMultiplier", true));
				col.Remove(col.Find("RiskRewardRatio", true));
				col.Remove(col.Find("Trail_frequency", true));
			}
		}			

		public void ModifyThreeStepTrailSetAutoProperties(PropertyDescriptorCollection col) {
			if (threeStepTrail == false) {
				col.Remove(col.Find("step1ProfitTrigger", true));
				col.Remove(col.Find("step2ProfitTrigger", true));
				col.Remove(col.Find("step3ProfitTrigger", true));
				col.Remove(col.Find("step1StopLoss", true));
				col.Remove(col.Find("step2StopLoss", true));
				col.Remove(col.Find("step3StopLoss", true));				
				col.Remove(col.Find("step1Frequency", true));
				col.Remove(col.Find("step2Frequency", true));
				col.Remove(col.Find("step3Frequency", true));				
			}
		}
		
		#endregion
		
		#region ICustomTypeDescriptor Members

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(GetType());
        }

        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(GetType());
        }

        public string GetComponentName()
        {
            return TypeDescriptor.GetComponentName(GetType());
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(GetType());
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(GetType());
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(GetType());
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(GetType(), editorBaseType);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(GetType(), attributes);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(GetType());
        }

        // Ensure GetProperties calls the right method
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            PropertyDescriptorCollection orig = TypeDescriptor.GetProperties(GetType(), attributes);
            PropertyDescriptor[] arr = new PropertyDescriptor[orig.Count];
            orig.CopyTo(arr, 0);
            PropertyDescriptorCollection col = new PropertyDescriptorCollection(arr);

            // Call modification methods IN ORDER
            ModifyProperties(col); // General modifications (like Timeframes)
			ModifyBESetAutoProperties(col); // BE modifications
            ModifyProfitTargetProperties(col); // Profit Target modifications <--- Uses the corrected logic
			ModifyTrailProperties(col); // Trail modifications
			ModifyTrailStopTypeProperties(col);
			ModifyTrailSetAutoProperties(col);
			ModifyThreeStepTrailSetAutoProperties(col);

            return col;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return TypeDescriptor.GetProperties(GetType());
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }
		#endregion		
	
		#region Properties

		#region 01a. Release Notes
		
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name="BaseAlgoVersion", Order=1, GroupName="01a. Release Notes")]
		public string BaseAlgoVersion
		{ get; set; }
		
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name="Author", Order=2, GroupName="01a. Release Notes")]
		public string Author
		{ get; set; }		
		
		[ReadOnly(true)]
		[NinjaScriptProperty]
//		[ReadOnly(true)]
		[Display(Name="StrategyName", Order=3, GroupName="01a. Release Notes")]
		public string StrategyName
		{ get; set; }
		
		[ReadOnly(true)]
		[NinjaScriptProperty]
//		[ReadOnly(true)]
		[Display(Name="Version", Order =4, GroupName="01a. Release Notes")]
		public string Version
		{ get; set; }
		
		[ReadOnly(true)]
		[NinjaScriptProperty]
//		[ReadOnly(true)]
		[Display(Name="Credits", Order=5, GroupName="01a. Release Notes")]
		public string Credits
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Chart Type", Order=6, GroupName="01a. Release Notes")]
		public string ChartType
		{ get; set; }
		
		#endregion
		
		#region 01b. Support Developer
		
		[NinjaScriptProperty]
		[Display(Name = "PayPal Donation URL", Order = 1, GroupName = "01b. Support Developer", Description = "https://www.paypal.com/signin")]
		public string paypal { get; set; }
		
		#endregion

		#region 02. Order Settings	
		
		[NinjaScriptProperty]
        [RefreshProperties(RefreshProperties.All)]
        [Display(Name="Enable Fixed Profit Target", Order=1, GroupName="02. Order Settings")]
        public bool EnableFixedProfitTarget
        {
            get { return enableFixedProfitTarget; }
            set
            {
                // Only process if the value is changing to true
                if (value && !enableFixedProfitTarget)
                {
                    enableFixedProfitTarget = true; // Set this one true
                    // Set others false directly using their backing fields
                    enableDynamicProfitTarget = false;
                    enableRegChanProfitTarget = false;
                    // Trigger UI update (essential when properties change affecting others)
                    if (Calculate == Calculate.OnEachTick || Calculate == Calculate.OnPriceChange) // Check if UI updates are relevant
                        ForceRefresh(); // Force parameter UI refresh
                }
                // Allow setting to false without forcing another default
                else if (!value)
                {
                    enableFixedProfitTarget = false;
                }
                 // If value is true but already true, do nothing
            }
        }

        [NinjaScriptProperty]
        [RefreshProperties(RefreshProperties.All)]
        [Display(Name="Enable RegChan Profit Target", Order=2, GroupName="02. Order Settings", Description="Uses Regression Channel bands as dynamic profit target.")] // Changed Order
        public bool EnableRegChanProfitTarget
        {
            get { return enableRegChanProfitTarget; } // Use backing field
            set
            {
                 if (value && !enableRegChanProfitTarget) // Check against backing field
                {
                     enableRegChanProfitTarget = true; // Set this one true
                     // Set others false
                     enableFixedProfitTarget = false;
                     enableDynamicProfitTarget = false;
                     if (Calculate == Calculate.OnEachTick || Calculate == Calculate.OnPriceChange)
                        ForceRefresh();
                }
                 else if (!value)
                 {
                     enableRegChanProfitTarget = false;
                 }
            }
        }
		
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Min RegChan Target Distance (Ticks)", Order=3, GroupName="02. Order Settings", Description="Minimum ticks required between entry and RegChan band for it to be used as target. Otherwise, falls back to 'Profit Target'.")]
        public int MinRegChanTargetDistanceTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Min RegChan Stop Distance (Ticks)", Order=4, GroupName="02. Order Settings", Description="Minimum ticks required between current price and RegChan band for it to be used as stop. Otherwise, falls back to 'Initial Stop' ticks for trailing.")]
        public int MinRegChanStopDistanceTicks { get; set; }

        [NinjaScriptProperty]
        [RefreshProperties(RefreshProperties.All)]
        [Display(Name="Enable Pivot Profit Target", Order=5, GroupName="02. Order Settings")] // Changed Order
        public bool EnableDynamicProfitTarget
        {
             get { return enableDynamicProfitTarget; }
             set
             {
                 if (value && !enableDynamicProfitTarget)
                {
                    enableDynamicProfitTarget = true; // Set this one true
                    // Set others false
                    enableFixedProfitTarget = false;
                    enableRegChanProfitTarget = false;
                    if (Calculate == Calculate.OnEachTick || Calculate == Calculate.OnPriceChange)
                        ForceRefresh();
                }
                 else if (!value)
                 {
                     enableDynamicProfitTarget = false;
                 }
             }
        }

		[NinjaScriptProperty]
        [Display(Name = "Order Type (Market/Limit)", Order = 6, GroupName = "02. Order Settings")]
        public OrderType OrderType { get; set; } 
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Limit Order Offset", Order= 7, GroupName="02. Order Settings")]
		public double LimitOffset
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Contracts", Order= 8, GroupName="02. Order Settings")]
		public int Contracts
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Tick Move (Button Click)", Order= 9, GroupName="02. Order Settings")]
		public int TickMove
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Initial Stop (Ticks)", Order= 10, GroupName="02. Order Settings")]
		public int InitialStop
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profit Target", Order=11, GroupName="02. Order Settings")]
		public double ProfitTarget
		{ get; set; }
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]	
		[Display(Name="Enable Profit Target 2", Order= 12, GroupName="02. Order Settings")]
		public bool EnableProfitTarget2
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Contract 2", Order= 13, GroupName="02. Order Settings")]
		public int Contracts2
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Display(Name="Profit Target 2", Order=14, GroupName="02. Order Settings")]
		public double ProfitTarget2
		{ get; set; }
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]	
		[Display(Name="Enable Profit Target 3", Order= 15, GroupName="02. Order Settings")]
		public bool EnableProfitTarget3
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Contract 3", Order= 16, GroupName="02. Order Settings")]
		public int Contracts3
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Display(Name="Profit Target3", Order=17, GroupName="02. Order Settings")]
		public double ProfitTarget3
		{ get; set; }
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]	
		[Display(Name="Enable Profit Target 4", Order= 18, GroupName="02. Order Settings")]
		public bool EnableProfitTarget4
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Contract 4", Order= 19, GroupName="02. Order Settings")]
		public int Contracts4
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Display(Name="Profit Target4", Order=20, GroupName="02. Order Settings")]
		public double ProfitTarget4
		{ get; set; }	
		
		#endregion	
		
		#region 03. Order Management
				
		[NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Stop Loss Type", Description= "Type of Trail Stop", GroupName = "03. Order Management", Order = 1)]
        [RefreshProperties(RefreshProperties.All)]
		public TrailStopTypeKC TrailStopType
        { 
			get { return trailStopType; } 
			set { 
				trailStopType = value; 
				if (trailStopType == TrailStopTypeKC.Tick_Trail) {
					tickTrail = true;
					enableFixedStopLoss = false;
					atrTrailSetAuto = false;
					showAtrTrailSetAuto = false;					
					showAtrTrailOptions = false;
					threeStepTrail = false;
					showThreeStepTrailOptions = false;
				}
				else if (trailStopType == TrailStopTypeKC.Fixed_Stop) {
					enableFixedStopLoss = true;
					atrTrailSetAuto = false;
					showAtrTrailSetAuto = false;					
					showAtrTrailOptions = false;
					tickTrail = false;
					threeStepTrail = false;
					showThreeStepTrailOptions = false;
				}
				else if (trailStopType == TrailStopTypeKC.ATR_Trail) {
					enableFixedStopLoss = false;
					atrTrailSetAuto = true;
					showAtrTrailSetAuto = true;					
					showAtrTrailOptions = true;
					tickTrail = false;
					threeStepTrail = false;
					showThreeStepTrailOptions = false;
				} else if (trailStopType == TrailStopTypeKC.Three_Step_Trail) {
//					TrailSetAuto = false;
					enableFixedStopLoss = false;
					threeStepTrail = true;
					showThreeStepTrailOptions = true;	
					showAtrTrailOptions = false;				
					atrTrailSetAuto = false;
					showAtrTrailSetAuto = false;	
					tickTrail = false;
				} 
                else if (trailStopType == TrailStopTypeKC.Regression_Channel_Trail) 
                {
                    enableTrail = true; // Enable trailing conceptually
                    // No extra parameters needed to show/hide specifically for this yet
                }
			}
		}

		[NinjaScriptProperty]
		[Display(Name="ATR Period", Order= 2, GroupName="03. Order Management")]
		public int AtrPeriod	
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="ATR Trailing Multiplier", Order= 3, GroupName="03. Order Management")]
		public double atrMultiplier
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Risk To Reward Ratio", Order= 4, GroupName="03. Order Management")]
		public double RiskRewardRatio
		{ get; set; }

//		[NinjaScriptProperty]
//		[Display(Name="Trail Frecuency (Ticks)", Order=6, GroupName="03. Order Management - 1. Tick")]
//		public int Trail_frequency
//		{ get; set; }	
		
		[NinjaScriptProperty]
		[Display(Name = "Enable ATR Profit Target", Description = "Enable  Profit Target based on TrendMagic", Order = 5, GroupName = "03. Order Management")]
		[RefreshProperties(RefreshProperties.All)]
		public bool enableAtrProfitTarget			
		{ get; set; }
		
		//Breakeven Actual				
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]	
		[Display(Name="Enable Breakeven", Order= 6, GroupName="03. Order Management")]	
		public bool BESetAuto
		{ 	get{
				return beSetAuto;
			} 
			set {
				beSetAuto = value;
				
				if (beSetAuto == true) {
					showctrlBESetAuto = true;
				} else {
					showctrlBESetAuto = false;
				}
			}
		}
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Breakeven Trigger", Order = 7, Description="In Ticks", GroupName="03. Order Management")]
		public int BE_Trigger
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Breakeven Offset", Order = 8, Description="In Ticks", GroupName="03. Order Management")]
		public int BE_Offset
		{ get; set; }		
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Background Color Signal", Description = "Enable Exit", Order = 9, GroupName = "03. Order Management")]
		[RefreshProperties(RefreshProperties.All)]
		public bool enableBackgroundSignal
		{ get; set; }
		
		// New 04/18/2025
        [NinjaScriptProperty]
		[Range(0, 360)]
		[Display(Name = "Background Opacity", Description = "Background Opacity", Order = 10, GroupName = "03. Order Management")]
		public byte Opacity { get; set; }		
		
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Exit", Description = "Enable Exit", Order = 11, GroupName = "03. Order Management")]
		[RefreshProperties(RefreshProperties.All)]
		public bool enableExit
		{ get; set; }
		
		
		#endregion			

		#region 04. Three-step Trailing Stop
		
		[NinjaScriptProperty]
		[Display(Name="Profit Trigger Step 1", Order = 1, GroupName="04. Three-step Trailing Stop")]
		public int step1ProfitTrigger
		{ get; set; }		
		
		[NinjaScriptProperty]
		[Display(Name="Stop Loss Step 1", Order = 2, GroupName="04. Three-step Trailing Stop")]
		public int step1StopLoss
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Profit Trigger Step 2", Order = 3, GroupName="04. Three-step Trailing Stop")]
		public int step2ProfitTrigger
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Stop Loss Step 2", Order = 4, GroupName="04. Three-step Trailing Stop")]
		public int step2StopLoss
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Profit Trigger Step 3", Order = 5, GroupName="04. Three-step Trailing Stop")]
		public int step3ProfitTrigger
		{ get; set; }		
		
		[NinjaScriptProperty]
		[Display(Name="Stop Loss Step 3", Order = 6, GroupName="04. Three-step Trailing Stop")]
		public int step3StopLoss
		{ get; set; }		
		
//		[NinjaScriptProperty]
//		[Display(Name="Step1Frequency", Order=7, GroupName="04. Three-step Trailing Stop")]
//		public int step1Frequency
//		{ get; set; }
		
//		[NinjaScriptProperty]
//		[Display(Name="Step2Frequency", Order=8, GroupName="04. Three-step Trailing Stop")]
//		public int step2Frequency
//		{ get; set; }			
		
//		[NinjaScriptProperty]
//		[Display(Name="Step 3 Frequency", Order=9, GroupName="04. Three-step Trailing Stop")]
//		public int step3Frequency
//		{ get; set; }	
		
		#endregion	
		
		#region 05. Profit/Loss Limit	
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Daily Loss / Profit ", Description = "Enable / Disable Daily Loss & Profit control", Order =1, GroupName = "05. Profit/Loss Limit	")]
		[RefreshProperties(RefreshProperties.All)]
		public bool dailyLossProfit
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name="Daily Profit Limit ($)", Description="No positive or negative sign, just integer", Order=2, GroupName="05. Profit/Loss Limit	")]
		public double DailyProfitLimit
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name="Daily Loss Limit ($)", Description="No positive or negative sign, just integer", Order=3, GroupName="05. Profit/Loss Limit	")]
		public double DailyLossLimit
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Trailing Drawdown", Description = "Enable / Disable trailing drawdown", Order =4, GroupName = "05. Profit/Loss Limit	")]
		[RefreshProperties(RefreshProperties.All)]
		public bool enableTrailingDrawdown
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name="Trailing Drawdown ($)", Description="No positive or negative sign, just integer", Order=5, GroupName="05. Profit/Loss Limit	")]
		public double TrailingDrawdown
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name="Start Trailing Drawdown ($)", Description="No positive or negative sign, just integer", Order=6, GroupName="05. Profit/Loss Limit	")]
		public double StartTrailingDD
		{ get; set; }	
		
		#endregion

		#region	06. Trades Per Direction	
		[NinjaScriptProperty]
		[Display(Name = "Enable Trades Per Direction", Description = "Switch off Historical Trades to use this option.", Order = 0, GroupName = "06. Trades Per Direction")]
		[RefreshProperties(RefreshProperties.All)]
		public bool TradesPerDirection 
		{
		 	get{return tradesPerDirection;} 
			set{tradesPerDirection = (value);} 
		}
		
		[NinjaScriptProperty]
		[Display(Name="Long Per Direction", Description = "Number of long in a row", Order = 1, GroupName = "06. Trades Per Direction")]
		public int longPerDirection
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Short Per Direction", Description = "Number of short in a row", Order = 2, GroupName = "06. Trades Per Direction")]
		public int shortPerDirection
		{ get; set; }

		#endregion
		
		#region 07. Other Trade Controls
		
		[NinjaScriptProperty]
		[Display(Name="Seconds Since Entry", Description = "Time between orders i seconds", Order = 3, GroupName = "07. Other Trade Controls")]
		public int SecsSinceEntry
		{ get; set; }				
		
		[NinjaScriptProperty]
		[Display(Name="Bars Since Exit", Description = "Number of bars that have elapsed since the last specified exit. 0 == Not used. >1 == Use number of bars specified ", Order=4, GroupName="07. Other Trade Controls" )]
		public int iBarsSinceExit
		{ get; set; }
		
		#endregion
		
		#region 08b. Default Settings			
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Buy Sell Pressure", Order = 1, GroupName = "08b. Default Settings")]
		public bool enableBuySellPressure { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show Buy Sell Pressure", Order = 2, GroupName = "08b. Default Settings")]
		public bool showBuySellPressure { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable VMA", Order = 3, GroupName = "08b. Default Settings")]
		public bool enableVMA { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show VMA", Order = 4, GroupName = "08b. Default Settings")]
		public bool showVMA { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Hooker", Order = 5, GroupName = "08b. Default Settings")]
		public bool enableHmaHooks { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show HMA Hooks", Order = 6, GroupName = "08b. Default Settings")]
		public bool showHmaHooks { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "HMA Period", Order = 7, GroupName = "08b. Default Settings")]
		public int HmaPeriod { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable KingKhanh", Order = 8, GroupName = "08b. Default Settings")]
		public bool enableRegChan1 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Inner Regression Channel", Order = 9, GroupName = "08b. Default Settings")]
		public bool enableRegChan2 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show Outer Regression Channel", Order = 10, GroupName = "08b. Default Settings")]
		public bool showRegChan1 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show Inner Regression Channel", Order = 11, GroupName = "08b. Default Settings")]
		public bool showRegChan2 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show High and Low Lines", Order = 12, GroupName = "08b. Default Settings")]
		public bool showRegChanHiLo { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Regression Channel Period", Order = 13, GroupName="08b. Default Settings")]
		public int RegChanPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Outer Regression Channel Width", Order = 14, GroupName="08b. Default Settings")]
		public double RegChanWidth
		{ get; set; }
			
		[NinjaScriptProperty]
		[Display(Name = "Inner Regression Channel Width", Order = 15, GroupName = "08b. Default Settings")]
		public double RegChanWidth2 { get; set; }
	
		[NinjaScriptProperty]
        [Display(Name = "Enable Momo", Order = 16, GroupName = "08b. Default Settings")]
        public bool enableMomo { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Momentum", Order = 17, GroupName = "08b. Default Settings")]
        public bool showMomo { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Momo Up", Order = 18, GroupName="08b. Default Settings")]
		public int MomoUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Momo Down", Order = 19, GroupName="08b. Default Settings")]
		public int MomoDown
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable ADX", Order = 20, GroupName = "08b. Default Settings")]
        public bool enableADX { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show ADX", Order = 21, GroupName = "08b. Default Settings")]
        public bool showAdx { get; set; }
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Period", Order = 22, GroupName = "08b. Default Settings")]
        public int adxPeriod { get; set; }
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Threshold 1", Order = 23, GroupName = "08b. Default Settings")]
        public int AdxThreshold { get; set; }
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Threshold 2", Order = 24, GroupName = "08b. Default Settings")]
        public int adxThreshold2 { get; set; }
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Exit Threshold", Order = 25, GroupName = "08b. Default Settings")]
        public int adxExitThreshold { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable Volatility", Order = 26, GroupName = "08b. Default Settings")]
        public bool enableVolatility { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name="Volatility Threshold", Order = 27, GroupName="08b. Default Settings")]
        public double atrThreshold { get; set; }		
		
		[NinjaScriptProperty]
        [Display(Name = "Enable EMA Filter", Order = 28, GroupName = "08b. Default Settings")]
        public bool enableEMAFilter { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show EMA", Order = 29, GroupName = "08b. Default Settings")]
        public bool showEMA { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="EMA Length", Order = 30, GroupName="08b. Default Settings")]
		public int emaLength
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Pivots", Order = 31, GroupName = "08b. Default Settings")]
        public bool showPivots { get; set; }
		
		#endregion	
		
		#region 09. Market Condition		
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Choppiness Detection", Order = 1, GroupName = "09. Market Condition")]
		public bool EnableChoppinessDetection { get; set; } 
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Regression Channel Look Back Period", Description="Period for Regression Channel used in chop detection.", Order=2, GroupName="09. Market Condition")]
		public int SlopeLookBack { get; set; }
		
		[NinjaScriptProperty]
		[Range(0.1, 1.0)] // Factor less than 1 to indicate narrower than average
		[Display(Name="Flat Slope Factor", Description="Factor of slope of Regression Channel indicates flatness.", Order=3, GroupName="09. Market Condition")]
		public double FlatSlopeFactor { get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Chop ADX Threshold", Description="ADX value below which the market is considered choppy (if RegChan is also flat).", Order=4, GroupName="09. Market Condition")]
		public int ChopAdxThreshold { get; set; }
		
		#endregion

		#region 10. Timeframes
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Trades", Order=1, GroupName="10. Timeframes")]
		public DateTime Start
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Trades", Order=2, GroupName="10. Timeframes")]
		public DateTime End
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 2", Description = "Enable 2 times.", Order=3, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time2
		{
		 	get{return isEnableTime2;} 
			set{isEnableTime2 = (value);} 
		}
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time 2", Order=4, GroupName="10. Timeframes")]
		public DateTime Start2
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time 2", Order=5, GroupName="10. Timeframes")]
		public DateTime End2
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 3", Description = "Enable 3 times.", Order=6, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time3
		{
		 	get{return isEnableTime3;} 
			set{isEnableTime3 = (value);} 
		}
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time 3", Order=7, GroupName="10. Timeframes")]
		public DateTime Start3
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time 3", Order=8, GroupName="10. Timeframes")]
		public DateTime End3
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 4", Description = "Enable 4 times.", Order=9, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time4
		{
		 	get{return isEnableTime4;} 
			set{isEnableTime4 = (value);} 
		}
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time 4", Order=10, GroupName="10. Timeframes")]
		public DateTime Start4
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time 4", Order=11, GroupName="10. Timeframes")]
		public DateTime End4
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 5", Description = "Enable 5 times.", Order=12, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time5
		{
		 	get{return isEnableTime5;} 
			set{isEnableTime5 = (value);} 
		}
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time 5", Order=13, GroupName="10. Timeframes")]
		public DateTime Start5
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time 5", Order=14, GroupName="10. Timeframes")]
		public DateTime End5
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 6", Description = "Enable 6 times.", Order =15, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time6
		{
		 	get{return isEnableTime6;} 
			set{isEnableTime6 = (value);} 
		}
		
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Start Time 6", Order=16, GroupName="10. Timeframes")]
		public DateTime Start6
		{ get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="End Time 6", Order=17, GroupName="10. Timeframes")]
		public DateTime End6
		{ get; set; }
		
		#endregion
		
		#region 11. Status Panel	
		
		[NinjaScriptProperty]
        [Display(Name = "Show Daily PnL", Order = 1, GroupName = "11. Status Panel")]
        public bool showDailyPnl { get; set; }			
		
		[XmlIgnore()]
		[Display(Name = "Daily PnL Color", Order = 2, GroupName = "11. Status Panel")]
		public Brush colorDailyProfitLoss
		{ get; set; }	
		
		[NinjaScriptProperty]
		[Display(Name="Daily PnL Position", Description = "Daily PNL Alert Position", Order = 3, GroupName = "11. Status Panel")]
		public TextPosition PositionDailyPNL
		{ get; set; }
		
		// Serialize our Color object
		[Browsable(false)]
		public string colorDailyProfitLossSerialize
		{
			get { return Serialize.BrushToString(colorDailyProfitLoss); }
   			set { colorDailyProfitLoss = Serialize.StringToBrush(value); }
		}
		
        [NinjaScriptProperty]
        [Display(Name = "Show STATUS PANEL", Order = 4, GroupName = "11. Status Panel")]
        public bool showPnl { get; set; }		

		[XmlIgnore()]
		[Display(Name = "STATUS PANEL Color", Order = 5, GroupName = "11. Status Panel")]
		public Brush colorPnl
		{ get; set; }				
		
		[NinjaScriptProperty]
		[Display(Name="STATUS PANEL Position", Description = "Status PNL Position", Order = 6, GroupName = "11. Status Panel")]
		public TextPosition PositionPnl		
		{ get; set; }	
		
		// Serialize our Color object
		[Browsable(false)]
		public string colorPnlSerialize
		{
			get { return Serialize.BrushToString(colorPnl); }
   			set { colorPnl = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Show Historical Trades", Description = "Show Historical Teorical Trades", Order= 7, GroupName="11. Status Panel")]
		public bool ShowHistorical
		{ get; set; }
		
        #endregion
		
		#region 12. WebHook

		[NinjaScriptProperty]
		[Display(Name="Activate Discord webhooks", Description="Activate One or more Discord webhooks", GroupName="11. Webhook", Order = 0)]
		public bool useWebHook { get; set; }		
		
//		[NinjaScriptProperty]
//		[Display(Name="Discord webhooks", Description="One or more Discord webhooks, separated by comma.", GroupName="11. Webhook", Order = 1)]
//		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
//		public string AccountName { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Discord webhooks", Description="One or more Discord webhooks, separated by comma.", GroupName="11. Webhook", Order = 2)]
		public string DiscordWebhooks
		{ get; set; }
		
		#endregion	
		
		#region Trailing Stop Type
		// Stop Loss Type
		public enum TrailStopTypeKC
		{
			Tick_Trail,			
            Regression_Channel_Trail,			
			Three_Step_Trail,			
			ATR_Trail,
			Fixed_Stop
		}
		#endregion
		
		#endregion
    }
}

/*
  // Only enter if at least 10 bars has passed since our last exit or if we have never traded yet
  if ((BarsSinceExitExecution() > iBarsSinceExit || BarsSinceExitExecution() == -1) && CrossAbove(SMA(10), SMA(20), 1))
      EnterLong();

*/