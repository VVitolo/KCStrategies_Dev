#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using BlueZ = NinjaTrader.NinjaScript.Indicators.BlueZ; // Alias for better readability
using RegressionChannel = NinjaTrader.NinjaScript.Indicators.RegressionChannel;
using WilliamR = NinjaTrader.NinjaScript.Indicators.TradeSaber_SignalMod.TOWilliamsTraderOracleSignalMOD;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public abstract class ATMAlgoBase : Strategy, ICustomTypeDescriptor //
    {
        #region Variables

        // ATM Strategy Variables
        private string atmStrategyId = string.Empty;
        private string orderId = string.Empty;
        private bool isAtmStrategyCreated = false;
        private DateTime lastEntryTime;

        // Indicator Variables
        private BlueZ.BlueZHMAHooks hullMAHooks;
        private bool hmaHooksUp;
        private bool hmaHooksDown;
        private bool hmaUp;
        private bool hmaDown;

        private BuySellPressure BuySellPressure1;
        private Series<double> buyPressure;
        private Series<double> sellPressure;
        private bool buyPressureUp;
        private bool sellPressureUp;

        private RegressionChannel RegressionChannel1, RegressionChannel2;
        private RegressionChannelHighLow RegressionChannelHighLow1;
        private bool regChanUp;
        private bool regChanDown;

        private VMA VMA1;
        private bool volMaUp;
        private bool volMaDown;

        private Momentum Momentum1;
        private double momentum;
        private bool momoUp;
        private bool momoDown;

        private ADX ADX1;
        private double currentAdx;
        private bool adxUp;

        private ATR ATR1;
        private bool atrUp;

        private bool marketIsChoppy;
        private bool autoDisabledByChop; // Tracks if Auto was turned off by the system due to chop
        private bool extraSeriesValid = true;

        // Trend Variables
        public bool uptrend;
        public bool downtrend;

        private bool priceUp;
        private bool priceDown;

        // Signal Variables
        public bool longSignal;
        public bool shortSignal;
        public bool exitLong;
        public bool exitShort;

        // Position Variables
        public bool isLong;
        public bool isShort;
        public bool isFlat;

        // Progress Tracking
        private double actualPnL;
        private bool trailingDrawdownReached = false;

        private double entryPrice;
        private double currentPrice;

        // Trade Direction Management
        private bool tradesPerDirection;
        private int counterLong;
        private int counterShort;

        // Quick Order Buttons
        private bool QuickLong;
        private bool QuickShort;
        private bool quickLongBtnActive;
        private bool quickShortBtnActive;

        // Time Management
        private bool isEnableTime2;
        private bool isEnableTime3;
        private bool isEnableTime4;
        private bool isEnableTime5;
        private bool isEnableTime6;

        // Strategy Enablement
        private bool isManualTradeEnabled = true; // Default to enabled
        private bool isAutoTradeEnabled = false;
        private bool strategyStopped = false; // Flag to prevent further actions after stopping
        private bool isLongEnabled = true; // Default to enabled
        private bool isShortEnabled = true; // Default to enabled

        // WPF Control Variables
        private RowDefinition addedRow;
        private ChartTab chartTab;
        private Chart chartWindow;
        private Grid chartTraderGrid,
            chartTraderButtonsGrid,
            lowerButtonsGrid;

        private Button manualBtn,
            autoBtn,
            longBtn,
            shortBtn,
            quickLongBtn,
            quickShortBtn;
        private Button closeBtn,
            panicBtn,
            paypalBtn;
        private bool panelActive;
        private TabItem tabItem;
        private Grid myGrid;

        // P&L Variables
        private double totalPnL;
        private double cumPnL;
        private double dailyPnL;
        private bool canTradeOK = true;
        private bool canTradeToday;

        private bool syncPnl;
        private double historicalTimeTrades; // Sync P&L
        private double dif; // To Calculate PNL sync
        private double cumProfit; // For real time pnl and pnl synchronization

        private bool restartPnL;

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
        private double maxProfit; // Stores the highest profit achieved
		
        #region Order Label Constants (Highly Recommended)

        // Define your order labels as constants.  This prevents typos and ensures consistency.
        private const string LongEntryLabel = "LE";
        private const string ShortEntryLabel = "SE";
        private const string QuickLongEntryLabel = "QLE";
        private const string QuickShortEntryLabel = "QSE";
        private const string Add1LongEntryLabel = "Add1LE";
        private const string Add1ShortEntryLabel = "Add1SE";
        // Add constants for other order labels as needed (e.g., "LE2", "SE2", "TrailingStop")

        #endregion

		#region Multi Time Series Variables and Properties
		// ─────────────────────────────
	    // NEW MULTI TIME SERIES VARIABLES
	    // ─────────────────────────────

	    // Dictionaries to map our additional data series timeframes (by their BarsArray index)
	    private Dictionary<int, int> timeframeToBarsIndex = new Dictionary<int, int>();
	    private List<int> enabledTimeframes = new List<int>();
	
	    // To store computed signal (true/false) from each added data series.
	    // The key is the BarsArray index (which is also stored in enabledTimeframes).
	    private Dictionary<int, bool> multiSeriesSignals = new Dictionary<int, bool>();
		
		// ─────────────────────────────
	    // Minimum count of additional series signals required for a valid entry.
		// ─────────────────────────────
	    [NinjaScriptProperty]
	    [Display(Name = "Min Required Time Series Signals", Order = 1, GroupName = "13. Multi Time Series Options")]
	    public int MinRequiredTimeSeriesSignals { get; set; } = 3;

		// ─────────────────────────────
	    // NEW PROPERTIES FOR MULTI DATA SERIES
	    // ─────────────────────────────
	
		#region Bools and default settings for Heiken Ashi MINUTE based conditions
		// Bools for Heiken Ashi based conditions
        [NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 1 minute", Order = 1, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi1min { get; set; } = false;

		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 2 minute", Order = 2, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi2min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 3 minute", Order = 3, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi3min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 5 minute", Order = 4, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi5min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 15 minute", Order = 5, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi15min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Custom minutes", Order = 6, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshiCustom { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Custom minutes value", Order = 7, GroupName = "13. Timeframes: Heiken Ashi - Minute Input")]
        public int HeikenAshiCustomMinutes { get; set; } = 30;
		
		// Endregion Bools for Heiken Ashi based conditions
		#endregion
		
		#region Bools and default settings for Heiken Ashi TICK based conditions
		// Bools for Heiken Ashi based conditions
		
		// Heiken Ashi Tick Series 1
        [NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 1", Order = 1, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 1 value", Order = 2, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries1Value { get; set; } = 150;

		// Heiken Ashi Tick Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 2", Order = 3, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 2 value", Order = 4, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries2Value { get; set; } = 300;
		
		// Heiken Ashi Tick Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 3", Order = 5, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 3 value", Order = 6, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries3Value { get; set; } = 500;
		
		// Heiken Ashi Tick Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 4", Order = 7, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 4 value", Order = 8, GroupName = "13. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries4Value { get; set; } = 1000;
		
		// Endregion Bools for Heiken Ashi based conditions
		#endregion
		
		
		
		#region Bools and default settings for NinzaRenko based conditions
		// Bools for Renko based conditions
		
		// NinzaRenko Series 1
        [NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 1", Order = 1, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 1 Brick Size", Order = 2, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries1BrickSize { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 1 Trend Threshold", Order = 3, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries1TrendThreshold { get; set; } = 1;
		
		// NinzaRenko Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 2", Order = 4, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 2 Brick Size", Order = 5, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries2BrickSize { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 2 Trend Threshold", Order = 6, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries2TrendThreshold { get; set; } = 5;
		
		// NinzaRenko Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 3", Order = 7, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 3 Brick Size", Order = 8, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries3BrickSize { get; set; } = 8;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 3 Trend Threshold", Order = 9, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries3TrendThreshold { get; set; } = 4;
		
		// NinzaRenko Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 4", Order = 10, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 4 Brick Size", Order = 11, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries4BrickSize { get; set; } = 18;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 4 Trend Threshold", Order = 12, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries4TrendThreshold { get; set; } = 3;
		
		// NinzaRenko Series 5
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 5", Order = 13, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 5 Brick Size", Order = 14, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries5BrickSize { get; set; } = 32;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 5 Trend Threshold", Order = 15, GroupName = "13. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries5TrendThreshold { get; set; } = 8;
		
		// End Region Bools and default settings for Renko based conditions
		#endregion
		
		
		#region Bools and default settings for RV Bars based conditions
		// Bools for Renko based conditions
		
		// RV Series 1
        [NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 1", Order = 1, GroupName = "13. Timeframes: RV Bars")]
        public bool UseRVBarsSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 1 Directional Bar Ticks", Order = 2, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries1DirectionBarTicks { get; set; } = 8;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 1 Reversal Bars", Order = 3, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries1ReversalBars { get; set; } = 4;
		
		// RV Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 2", Order = 4, GroupName = "13. Timeframes: RV Bars")]
        public bool UseRVBarsSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 2 Directional Bar Ticks", Order = 5, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries2DirectionBarTicks { get; set; } = 5;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 2 Reversal Bars", Order = 6, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries2ReversalBars { get; set; } = 12;
		
		// RV Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 3", Order = 7, GroupName = "13. Timeframes: RV Bars")]
        public bool UseRVBarsSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 3 Directional Bar Ticks", Order = 8, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries3DirectionBarTicks { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 3 Reversal Bars", Order = 9, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries3ReversalBars { get; set; } = 8;
		
		// RV Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 4", Order = 10, GroupName = "13. Timeframes: RV Bars")]
        public bool UseRVBarsSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 4 Directional Bar Ticks", Order = 11, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries4DirectionBarTicks { get; set; } = 12;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 4 Reversal Bars", Order = 12, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries4ReversalBars { get; set; } = 8;
		
		// RV Series 5
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 5", Order = 13, GroupName = "13. Timeframes: RV Bars")]
        public bool UseRVBarsSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 5 Directional Bar Ticks", Order = 14, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries5DirectionBarTicks { get; set; } = 15;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 5 Reversal Bars", Order = 15, GroupName = "13. Timeframes: RV Bars")]
        public int RVBarsSeries5ReversalBars { get; set; } = 6;
		
		// End Region Bools and default settings for RV Bars based conditions
		#endregion
		
		
		#region Bools and default settings for ORenko based conditions
		// Bools for Orenko based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use ORenko Series 1", Order = 1, GroupName = "13. Timeframes: Renko - Orenko")]
        public bool UseORenkoSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "ORenko Series 1 Trend Threshold Value", Order = 2, GroupName = "13. Timeframes: Renko - Orenko")]
        public int ORenkoSeries1TrendThresholdValue { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "ORenko Series 1 Open Offset Value", Order = 3, GroupName = "13. Timeframes: Renko - Orenko")]
        public int ORenkoSeries1OpenOffsetValue { get; set; } = 12;
		
		[NinjaScriptProperty]
        [Display(Name = "ORenko Series 1 Reversal Threshold Value", Order = 4, GroupName = "13. Timeframes: Renko - Orenko")]
        public int ORenkoSeries1ReversalThresholdValue { get; set; } = 28;
		
		#endregion
		
		
		#region Bools and default settings for Unirenko based conditions
		// Bools for Unirenko based conditions
		
		// Unirenko Series 1
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 1", Order = 1, GroupName = "13. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 1 Tick Trend Value", Order = 2, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries1TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 1 Open Offset Value", Order = 3, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries1OpenOffsetValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 1 Tick Reversal Value", Order = 4, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries1TickReversalValue { get; set; } = 10;
		
		// Unirenko Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 2", Order = 5, GroupName = "13. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 2 Tick Trend Value", Order = 6, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries2TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 2 Open Offset Value", Order = 7, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries2OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 2 Tick Reversal Value", Order = 8, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries2TickReversalValue { get; set; } = 20;
		
		// Unirenko Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 3", Order = 9, GroupName = "13. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 3 Tick Trend Value", Order = 10, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries3TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 3 Open Offset Value", Order = 11, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries3OpenOffsetValue { get; set; } = 44;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 3 Tick Reversal Value", Order = 12, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries3TickReversalValue { get; set; } = 44;
		
		// Unirenko Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 4", Order = 13, GroupName = "13. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries4 { get; set; }

		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 4 Tick Trend Value", Order = 14, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries4TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 4 Open Offset Value", Order = 15, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries4OpenOffsetValue { get; set; } = 50;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 4 Tick Reversal Value", Order = 16, GroupName = "13. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries4TickReversalValue { get; set; } = 200;
		
		// Endregion Bools and default settings for Unirenko based conditions
		#endregion
		
		
		#region Bools and default settings for UnirenkoHA based conditions
		// Bools for UnirenkoHA based conditions
		
		// UnirenkoHA Series 1
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 1", Order = 1, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 1 Tick Trend Value", Order = 2, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries1TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 1 Open Offset Value", Order = 3, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries1OpenOffsetValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 1 Tick Reversal Value", Order = 4, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries1TickReversalValue { get; set; } = 10;
		
		// UnirenkoHA Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 2", Order = 10, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 2 Tick Trend Value", Order = 11, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries2TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 2 Open Offset Value", Order = 12, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries2OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 2 Tick Reversal Value", Order = 13, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries2TickReversalValue { get; set; } = 30;
		
		// UnirenkoHA Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 3", Order = 20, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 3 Tick Trend Value", Order = 21, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries3TickTrendValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 3 Open Offset Value", Order = 22, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries3OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 3 Tick Reversal Value", Order = 23, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries3TickReversalValue { get; set; } = 50;
		
		// UnirenkoHA Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 4", Order = 30, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 4 Tick Trend Value", Order = 31, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries4TickTrendValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 4 Open Offset Value", Order = 32, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries4OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 4 Tick Reversal Value", Order = 33, GroupName = "13. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries4TickReversalValue { get; set; } = 100;
		// Endregion Bools and default settings for UnirenkoHA based conditions
		#endregion
		
		
		
		#region Bools and default settings for volume based conditions
		// Bools for volume based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 1", Order = 1, GroupName = "13. Timeframes: Volume")]
        public bool UseVolumeSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 1 Value", Order = 2, GroupName = "13. Timeframes: Volume")]
        public int VolumeSeries1Value { get; set; } = 250;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 2", Order = 3, GroupName = "13. Timeframes: Volume")]
        public bool UseVolumeSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 2 Value", Order = 4, GroupName = "13. Timeframes: Volume")]
        public int VolumeSeries2Value { get; set; } = 500;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 3", Order = 5, GroupName = "13. Timeframes: Volume")]
        public bool UseVolumeSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 3 Value", Order = 6, GroupName = "13. Timeframes: Volume")]
        public int VolumeSeries3Value { get; set; } = 1000;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 4", Order = 7, GroupName = "13. Timeframes: Volume")]
        public bool UseVolumeSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 4 Value", Order = 8, GroupName = "13. Timeframes: Volume")]
        public int VolumeSeries4Value { get; set; } = 2000;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 5", Order = 9, GroupName = "13. Timeframes: Volume")]
        public bool UseVolumeSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 5 Value", Order = 10, GroupName = "13. Timeframes: Volume")]
        public int VolumeSeries5Value { get; set; } = 5000;
		
		// Endregion Bools for volume based conditions
		#endregion

		
		#region Bools and default settings for range based conditions
		// Bools for range based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 1", Order = 1, GroupName = "13. Timeframes: Range")]
        public bool UseRangeSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 1 Value", Order = 2, GroupName = "13. Timeframes: Range")]
        public int RangeSeries1Value { get; set; } = 2;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 2", Order = 3, GroupName = "13. Timeframes: Range")]
        public bool UseRangeSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 2 Value", Order = 4, GroupName = "13. Timeframes: Range")]
        public int RangeSeries2Value { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 3", Order = 5, GroupName = "13. Timeframes: Range")]
        public bool UseRangeSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 3 Value", Order = 6, GroupName = "13. Timeframes: Range")]
        public int RangeSeries3Value { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 4", Order = 7, GroupName = "13. Timeframes: Range")]
        public bool UseRangeSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 4 Value", Order = 8, GroupName = "13. Timeframes: Range")]
        public int RangeSeries4Value { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 5", Order = 9, GroupName = "13. Timeframes: Range")]
        public bool UseRangeSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 5 Value", Order = 10, GroupName = "13. Timeframes: Range")]
        public int RangeSeries5Value { get; set; } = 50;
		
		// Endregion Bools and default settings for range based conditions
		#endregion
		
		
		#region Bools and default settings for MEAN range based conditions
		// Bools for MEAN range based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 1", Order = 1, GroupName = "13. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 1 Value", Order = 2, GroupName = "13. Timeframes: Range - MEAN")]
        public int MEANRangeSeries1Value { get; set; } = 2;
		
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 2", Order = 3, GroupName = "13. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 2 Value", Order = 4, GroupName = "13. Timeframes: Range - MEAN")]
        public int MEANRangeSeries2Value { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 3", Order = 5, GroupName = "13. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 3 Value", Order = 6, GroupName = "13. Timeframes: Range - MEAN")]
        public int MEANRangeSeries3Value { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 4", Order = 7, GroupName = "13. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 4 Value", Order = 8, GroupName = "13. Timeframes: Range - MEAN")]
        public int MEANRangeSeries4Value { get; set; } = 30;

		// Endregion Bools and default settings for MEAN range based conditions
		#endregion
		
		
		#region Bools and default settings for tick based conditions
		// Bools for tick based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 1", Order = 1, GroupName = "13. Timeframes: Tick")]
        public bool UseTickSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 1 Value", Order = 2, GroupName = "13. Timeframes: Tick")]
        public int UseTickSeries1Value { get; set; } = 200;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 2", Order = 3, GroupName = "13. Timeframes: Tick")]
        public bool UseTickSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 2 Value", Order = 4, GroupName = "13. Timeframes: Tick")]
        public int UseTickSeries2Value { get; set; } = 500;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 3", Order = 5, GroupName = "13. Timeframes: Tick")]
        public bool UseTickSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 3 Value", Order = 6, GroupName = "13. Timeframes: Tick")]
        public int UseTickSeries3Value { get; set; } = 1000;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 4", Order = 7, GroupName = "13. Timeframes: Tick")]
        public bool UseTickSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 4 Value", Order = 8, GroupName = "13. Timeframes: Tick")]
        public int UseTickSeries4Value { get; set; } = 1500;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 5", Order = 9, GroupName = "13. Timeframes: Tick")]
        public bool UseTickSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 5 Value", Order = 10, GroupName = "13. Timeframes: Tick")]
        public int UseTickSeries5Value { get; set; } = 2000;
		
		// Endregion Bools and default settings for tick based conditions
		#endregion
		
		
		#region Bools and default settings for TBars based conditions
		// Bools and settings for TBars based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 1", Order = 1, GroupName = "13. Timeframes: TBars")]
        public bool UseTBarsSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 1 Bar Speed", Order = 2, GroupName = "13. Timeframes: TBars")]
        public int UseTBarsSeries1BarSpeedValue { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 2", Order = 3, GroupName = "13. Timeframes: TBars")]
        public bool UseTBarsSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 2 Bar Speed", Order = 4, GroupName = "13. Timeframes: TBars")]
        public int UseTBarsSeries2BarSpeedValue { get; set; } = 12;
		
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 3", Order = 5, GroupName = "13. Timeframes: TBars")]
        public bool UseTBarsSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 3 Bar Speed", Order = 6, GroupName = "13. Timeframes: TBars")]
        public int UseTBarsSeries3BarSpeedValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 4", Order = 7, GroupName = "13. Timeframes: TBars")]
        public bool UseTBarsSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 4 Bar Speed", Order = 8, GroupName = "13. Timeframes: TBars")]
        public int UseTBarsSeries4BarSpeedValue { get; set; } = 34;
		
		
		
		// Endregion Bools and default settings for TBars based conditions
		#endregion
		
		
		#region Bools and default settings for Delta Bars based conditions
		// NOTE: The Delta isn't working correctly as of 2025/02/08 JET
		// Bools for Delta Bars based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 1", Order = 1, GroupName = "13. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 1 Trend Delta ", Order = 2, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries1TrendDeltaValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 1 Trend Reversal ", Order = 3, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries1TrendReversalValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 2", Order = 4, GroupName = "13. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 2 Trend Delta ", Order = 5, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries2TrendDeltaValue { get; set; } = 30;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 2 Trend Reversal ", Order = 6, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries2TrendReversalValue { get; set; } = 30;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 3", Order = 7, GroupName = "13. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 3 Trend Delta ", Order = 8, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries3TrendDeltaValue { get; set; } = 40;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 3 Trend Reversal ", Order = 9, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries3TrendReversalValue { get; set; } = 40;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 4", Order = 10, GroupName = "13. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 4 Trend Delta ", Order = 11, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries4TrendDeltaValue { get; set; } = 50;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 4 Trend Reversal ", Order = 12, GroupName = "13. Timeframes: Delta Bars")]
        public int DeltaBarsSeries4TrendReversalValue { get; set; } = 50;
		
		// Endregion Bools and default settings for Delta Bars based conditions
		#endregion
		
		
		#region Bools and default settings for Line Break for MINUTE based charts
		// This section is meant to specify line break data series. Users will specify the MINUTE value, and the number of line breaks.
		// A separate section will need to be created for other data series, such as the Line Break of Tick, or the Line Break of Volume
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 1", Order = 1, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 1, Minute value ", Order = 2, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries1MinuteValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 1, # Breaks Value ", Order = 3, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries1BreaksValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 2", Order = 4, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 2, Minute value ", Order = 5, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries2MinuteValue { get; set; } = 2;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 2, # Breaks Value ", Order = 6, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries2BreaksValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 3", Order = 7, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 3, Minute value ", Order = 8, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries3MinuteValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 3, # Breaks Value ", Order = 9, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries3BreaksValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 4", Order = 10, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 4, Minute value ", Order = 11, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries4MinuteValue { get; set; } = 5;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 4, # Breaks Value ", Order = 12, GroupName = "13. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries4BreaksValue { get; set; } = 3;
		
		// Endregion Bools and default settings for Line Break for MINUTE based charts
		#endregion
		
		
		#region Bools and default settings for Line Break for TICK based charts
		// This section is meant to specify line break data series. Users will specify the TICK value, and the number of line breaks.
		// A separate section will need to be created for other data series, such as the Line Break of Tick, or the Line Break of Volume
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break TICK Series 1", Order = 1, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public bool UseLineBreakTickBasedSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 1, TICK value ", Order = 2, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries1TickValue { get; set; } = 100;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 1, # Breaks Value ", Order = 3, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries1BreaksValue { get; set; } = 3;
		
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break TICK Series 2", Order = 4, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public bool UseLineBreakTickBasedSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 2, TICK value ", Order = 5, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries2TickValue { get; set; } = 250;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 2, # Breaks Value ", Order = 6, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries2BreaksValue { get; set; } = 3;
		
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break TICK Series 3", Order = 7, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public bool UseLineBreakTickBasedSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 3, TICK value ", Order = 8, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries3TickValue { get; set; } = 500;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 3, # Breaks Value ", Order = 9, GroupName = "13. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries3BreaksValue { get; set; } = 3;
		
		// Endregion Bools and default settings for Line Break for TICK based charts
		#endregion
		
		
		#region Bools and default settings for Second and Minute based conditions
		// Bools for Second and Minute based conditions
        [NinjaScriptProperty]
        [Display(Name = "Use 30 Second", Order = 1, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use30Second { get; set; }

		[NinjaScriptProperty]
        [Display(Name = "Use 1 Minute", Order = 2, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use1Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 2 Minute", Order = 3, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use2Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 3 Minute", Order = 4, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use3Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 5 Minute", Order = 5, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use5Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 10 Minute", Order = 6, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use10Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 15 Minute", Order = 7, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use15Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 30 Minute", Order = 8, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use30Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 1 Hour", Order = 9, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use1Hour { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 4 Hour", Order = 10, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool Use4Hour { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Custom (enter in Minutes)", Order = 11, GroupName = "13. Timeframes: Timeframes Minute")]
        public bool UseMinuteBasedCustom { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Custom: # Minutes", Order = 12, GroupName = "13. Timeframes: Timeframes Minute")]
        public int UseMinuteBasedCustomIntValue { get; set; } = 135;
		
		// Endregion for Bools and default settings for Second and Minute based conditions
		#endregion		
		
		//endregion Multi Time Series Variables and Properties
		#endregion


        private Account chartTraderAccount;
        private AccountSelector accountSelector;

        #region TradeToDiscord

        //		private ClientWebSocket clientWebSocket;
        //		private List<dynamic> signalHistory = new List<dynamic>();
        //		private DateTime lastDiscordMessageTime = DateTime.MinValue;
        //		private readonly TimeSpan discordRateLimitInterval = TimeSpan.FromSeconds(30); // Adjust the interval as needed

        //		private string lastSignalType = "N/A";
        //		private double lastEntryPrice = 0.0;
        //		private double lastStopLoss = 0.0;
        //		private double lastProfitTarget = 0.0;
        //		private DateTime lastSignalTime = DateTime.MinValue;

        #endregion

        #endregion

        public override string DisplayName
        {
            get { return Name; }
        }

        #region OnStateChange
        protected override void OnStateChange()
        {
            switch (State)
            {
                case State.SetDefaults:
                    ConfigureStrategyDefaults();
                    break;
                case State.Configure:
                    ConfigureStrategy();
                    break;
                case State.DataLoaded:
                    initializeIndicators();
                    LoadChartTraderButtons();
                    maxProfit = totalPnL;
                    break;
                case State.Historical:
                    break;
                case State.Terminated:
                    CleanUpStrategy();
                    break;
            }
        }

        private void ConfigureStrategyDefaults()
        {
            Description =
                @"Base Strategy with OEB v.5.0.2 TradeSaber(Dre). and ArchReactor for KC (Khanh Nguyen)";
            Name = "ATM AlgoBase";
            BaseAlgoVersion = "ATM AlgoBase v5.3";
            Author = "indiVGA, Khanh Nguyen, Oshi, MarketMath, based on ArchReactor";
            Version = "Version 5.3 Apr. 2025";
            Credits = "";
            StrategyName = "";
            ChartType = "Orenko 34-40-40"; // TODO: Document Magic Numbers
            paypal = "https://www.paypal.com/signin";

            EntriesPerDirection = 10;
            Calculate = Calculate.OnEachTick;
            EntryHandling = EntryHandling.AllEntries;
            IsExitOnSessionCloseStrategy = true;
            ExitOnSessionCloseSeconds = 30;
            IsFillLimitOnTouch = false;
            MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
            OrderFillResolution = OrderFillResolution.High;
            Slippage = 0;
            StartBehavior = StartBehavior.WaitUntilFlat;
            TimeInForce = TimeInForce.Gtc;
            TraceOrders = false;
            RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseIgnoreRejects;
            StopTargetHandling = StopTargetHandling.PerEntryExecution;
            BarsRequiredToTrade = 50;
            IsInstantiatedOnEachOptimizationIteration = false;

            // Default Parameters
            isAutoTradeEnabled = true;
            isManualTradeEnabled = false;
            isLongEnabled = true;
            isShortEnabled = true;
            canTradeOK = true;
            enableExit = false;

            orderType = OrderType.Limit;
            //ATMStrategyTemplate = "ATM";
            ATMStrategyTemplate = String.Empty; // Sudo Mod 4.12.25

            // Choppiness Defaults
            SlopeLookBack = 4;
            FlatSlopeFactor = 0.125;
            ChopAdxThreshold = 20;
            EnableChoppinessDetection = true;
            marketIsChoppy = false;
            autoDisabledByChop = false;
            enableBackgroundSignal = true;

            enableBuySellPressure = true;
            showBuySellPressure = false;

            HmaPeriod = 16;
            enableHmaHooks = true;
            showHmaHooks = true;

            RegChanPeriod = 40;
            RegChanWidth = 4;
            RegChanWidth2 = 3;
            enableRegChan1 = true;
            enableRegChan2 = true;
            showRegChan1 = true;
            showRegChan2 = true;
            showRegChanHiLo = true;

            enableVMA = true;
            showVMA = true;

            MomoUp = 1;
            MomoDown = -1;
            enableMomo = true;
            showMomo = false;

            enableADX = true;
            showAdx = false;
            adxPeriod = 7;
            adxThreshold = 25;
            adxThreshold2 = 50;

            AtrPeriod = 14;
            atrThreshold = 1.5;
            enableVolatility = true;

            LimitOffset = 2;
            TickMove = 4;
            BreakevenOffset = 4;

            tradesPerDirection = false;
            longPerDirection = 5;
            shortPerDirection = 5;

            QuickLong = false;
            QuickShort = false;

            counterLong = 0;
            counterShort = 0;

            Start = DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
            End = DateTime.Parse("07:30", System.Globalization.CultureInfo.InvariantCulture);
            Start2 = DateTime.Parse("07:31", System.Globalization.CultureInfo.InvariantCulture);
            End2 = DateTime.Parse("08:00", System.Globalization.CultureInfo.InvariantCulture);
            Start3 = DateTime.Parse("08:01", System.Globalization.CultureInfo.InvariantCulture);
            End3 = DateTime.Parse("12:00", System.Globalization.CultureInfo.InvariantCulture);
            Start4 = DateTime.Parse("12:01", System.Globalization.CultureInfo.InvariantCulture);
            End4 = DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
            Start5 = DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
            End5 = DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
            Start6 = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
            End6 = DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);

            // Panel Status
            showDailyPnl = true;
            PositionDailyPNL = TextPosition.BottomLeft;
            colorDailyProfitLoss = Brushes.Cyan;

            showPnl = false;
            PositionPnl = TextPosition.TopLeft;
            colorPnl = Brushes.Yellow;

            // PnL Daily Limits
            dailyLossProfit = true;
            DailyProfitLimit = 100000;
            DailyLossLimit = 1000;
            TrailingDrawdown = 1000;
            StartTrailingDD = 3000;
            maxProfit = double.MinValue;
            enableTrailingDD = true;
        }

        private void ConfigureStrategy()
        {
            RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
            buyPressure = new Series<double>(this);
            sellPressure = new Series<double>(this);
        }

		private void ConfigureMultiTimeSeries()
		{
        	int index = 1;  // BarsArray[0] is the primary series, additional series indices start at 1

				#region Add All Data Series
				
				#region UseHeikenAshi MINUTE Add Data Series
				if (UseHeikenAshi1min)
				{
				    AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 1, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				if (UseHeikenAshi2min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 2, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshi3min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 3, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshi5min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 5, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshi15min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 1, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshiCustom) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, HeikenAshiCustomMinutes, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion UseHeikenAshi MINUTE Add Data Series
				#endregion
				
				#region UseHeikenAshi TICK Add Data Series
				if (UseHeikenTICKSeries1)
				{
				    AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries1Value, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				if (UseHeikenTICKSeries2) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries2Value, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenTICKSeries3) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries3Value, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenTICKSeries4) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries4Value, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion UseHeikenAshi MINUTE Add Data Series
				#endregion
				
				#region NinzaRenko Add Data Series
				if (UseNinzaRenkoSeries1)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					// AddRenko(Bars, 10);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries1BrickSize, Value2 = NinzaRenkoSeries1TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries2)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries2BrickSize, Value2 = NinzaRenkoSeries2TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries3)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries3BrickSize, Value2 = NinzaRenkoSeries3TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries4)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries4BrickSize, Value2 = NinzaRenkoSeries4TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries5)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries5BrickSize, Value2 = NinzaRenkoSeries5TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion NinzaRenko Add Data Series
				#endregion

				#region RV Bars Add Data Series
				if (UseRVBarsSeries1)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries1DirectionBarTicks, Value2 = RVBarsSeries1ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries2DirectionBarTicks, Value2 = RVBarsSeries2ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries3)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries3DirectionBarTicks, Value2 = RVBarsSeries3ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries4)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries4DirectionBarTicks, Value2 = RVBarsSeries4ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries5)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries5DirectionBarTicks, Value2 = RVBarsSeries5ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion RV Bars Add Data Series
				#endregion
				
				#region Unirenko Add Data Series
				if (UseUnirenkoSeries1)
				{
					// (BarsPeriodType)2018 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries1OpenOffsetValue, Value = UnirenkoSeries1TickTrendValue, Value2 = UnirenkoSeries1TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoSeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries2OpenOffsetValue, Value = UnirenkoSeries2TickTrendValue, Value2 = UnirenkoSeries2TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoSeries3)
				{
					// (BarsPeriodType)2018 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries3OpenOffsetValue, Value = UnirenkoSeries3TickTrendValue, Value2 = UnirenkoSeries3TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoSeries4)
				{
					// (BarsPeriodType)2018 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries4OpenOffsetValue, Value = UnirenkoSeries4TickTrendValue, Value2 = UnirenkoSeries4TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Unirenko Add Data Series		
				#endregion
				
				#region UnirenkoHA Add Data Series
				if (UseUnirenkoHASeries1)
				{
					// (BarsPeriodType)2021 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries1OpenOffsetValue, Value = UnirenkoHASeries1TickTrendValue, Value2 = UnirenkoHASeries1TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoHASeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries2OpenOffsetValue, Value = UnirenkoHASeries2TickTrendValue, Value2 = UnirenkoHASeries2TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoHASeries3)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries3OpenOffsetValue, Value = UnirenkoHASeries3TickTrendValue, Value2 = UnirenkoHASeries3TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoHASeries4)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries4OpenOffsetValue, Value = UnirenkoHASeries4TickTrendValue, Value2 = UnirenkoHASeries4TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion UnirenkoHA Add Data Series		
				#endregion
				
				#region Volume Add Data Series
				if (UseVolumeSeries1)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries1Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries2)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries2Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries3)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries3Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries4)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries4Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries5)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries5Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Volume Add Data Series
				#endregion
				
				#region Range Bar Add Data Series
				if (UseRangeSeries1)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries1Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries2)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries2Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries3)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries3Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries4)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries4Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries5)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries5Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Range Bar Add Data Series
				#endregion
				
				#region MEANRange Bar Add Data Series
				if (UseMEANRangeSeries1)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries1Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMEANRangeSeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries2Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMEANRangeSeries3)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries3Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMEANRangeSeries4)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries4Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion MEANRange Bar Add Data Series
				#endregion
				
				#region Tick Add Data Series
				if (UseTickSeries1)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries1Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTickSeries2)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries2Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTickSeries3)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries3Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTickSeries4)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries4Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseTickSeries5)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries5Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Tick Add Data Series
				#endregion
				
				#region TBars Add Data Series
				if (UseTBarsSeries1)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries1BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTBarsSeries2)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries2BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseTBarsSeries3)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries3BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTBarsSeries4)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries4BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				
				
				// Endregion Tbars Add Data Series
				#endregion
				
				#region ORenko Add Data Series
				if (UseORenkoSeries1)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2023, BarsPeriodTypeName = "ORenko", BaseBarsPeriodValue = ORenkoSeries1TrendThresholdValue, Value = ORenkoSeries1OpenOffsetValue, Value2 = ORenkoSeries1ReversalThresholdValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				
				// Endregion ORenko Add Data Series
				#endregion
				
				#region Delta Bars Add Data Series
				// Delta bars are based on Tick data, so the tick data has to be added
				if (UseDeltaBarsSeries1)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries1TrendDeltaValue, Value2 = DeltaBarsSeries1TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}

				if (UseDeltaBarsSeries2)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries2TrendDeltaValue, Value2 = DeltaBarsSeries2TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}
				
				if (UseDeltaBarsSeries3)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries3TrendDeltaValue, Value2 = DeltaBarsSeries3TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}
				
				if (UseDeltaBarsSeries4)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries4TrendDeltaValue, Value2 = DeltaBarsSeries4TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}
				
				#endregion
				
				#region Line Break MINUTES Add Data Series
				// Add the line break data series. This uses a built-in method similar to Heiken Ashi, as referenced here: https://ninjatrader.com/support/helpguides/nt8/NT%20HelpGuide%20English.html?adddataseries.htm
				
				
				if (UseLineBreakMINUTEBasedSeries1)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries1MinuteValue, LineBreakMinutesBasedSeries1BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}


				if (UseLineBreakMINUTEBasedSeries2)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries2MinuteValue, LineBreakMinutesBasedSeries2BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				if (UseLineBreakMINUTEBasedSeries3)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries3MinuteValue, LineBreakMinutesBasedSeries3BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				if (UseLineBreakMINUTEBasedSeries4)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries4MinuteValue, LineBreakMinutesBasedSeries4BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				
				
				#endregion
				
				#region Line Break TICK Add Data Series
				// Add the line break data series. This uses a built-in method similar to Heiken Ashi, as referenced here: https://ninjatrader.com/support/helpguides/nt8/NT%20HelpGuide%20English.html?adddataseries.htm
				if (UseLineBreakTickBasedSeries1)
				{
				    AddLineBreak(Instrument.FullName, BarsPeriodType.Tick, LineBreakTickBasedSeries1TickValue, LineBreakTickBasedSeries1BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}


				if (UseLineBreakTickBasedSeries2)
				{
				    AddLineBreak(Instrument.FullName, BarsPeriodType.Tick, LineBreakTickBasedSeries2TickValue, LineBreakTickBasedSeries2BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				if (UseLineBreakTickBasedSeries3)
				{
				    AddLineBreak(Instrument.FullName, BarsPeriodType.Tick, LineBreakTickBasedSeries3TickValue, LineBreakTickBasedSeries3BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				// endregion Line Break TICK Add Data Series
				#endregion
				
				#region Second and Minute Add Data Series
				// Add Second and Minute data series.
				if (Use30Second) 
				{
		            AddDataSeries(BarsPeriodType.Second, 30);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use1Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 1);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use2Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 2);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use3Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 3);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use5Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 5);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
				}
				if (Use10Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 10);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use15Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 15);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use30Minute)
				{
		            AddDataSeries(BarsPeriodType.Minute, 30);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use1Hour)
				{
		            AddDataSeries(BarsPeriodType.Minute, 60);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use4Hour)
				{
		            AddDataSeries(BarsPeriodType.Minute, 240);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMinuteBasedCustom)
				{
		            AddDataSeries(BarsPeriodType.Minute, UseMinuteBasedCustomIntValue);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Second and Minute Add Data Series
				#endregion
				
				#endregion Add All Data Series
		}

        private void initializeIndicators()
        {
            hullMAHooks = BlueZHMAHooks(
                Close,
                HmaPeriod,
                0,
                false,
                false,
                true,
                Brushes.Lime,
                Brushes.Red
            );
            hullMAHooks.Plots[0].Brush = Brushes.White;
            hullMAHooks.Plots[0].Width = 2;
            if (showHmaHooks)
                AddChartIndicator(hullMAHooks);

            RegressionChannel1 = RegressionChannel(Close, RegChanPeriod, RegChanWidth);
            if (showRegChan1)
                AddChartIndicator(RegressionChannel1);

            RegressionChannel2 = RegressionChannel(Close, RegChanPeriod, RegChanWidth2);
            if (showRegChan2)
                AddChartIndicator(RegressionChannel2);

            RegressionChannelHighLow1 = RegressionChannelHighLow(
                Close,
                RegChanPeriod,
                RegChanWidth
            );
            if (showRegChanHiLo)
                AddChartIndicator(RegressionChannelHighLow1);

            BuySellPressure1 = BuySellPressure(Close);
            BuySellPressure1.Plots[0].Width = 2;
            BuySellPressure1.Plots[0].Brush = Brushes.Lime;
            BuySellPressure1.Plots[1].Width = 2;
            BuySellPressure1.Plots[1].Brush = Brushes.Red;
            if (showBuySellPressure)
                AddChartIndicator(BuySellPressure1);

            Momentum1 = Momentum(Close, 14);
            Momentum1.Plots[0].Brush = Brushes.Yellow;
            Momentum1.Plots[0].Width = 2;
            if (showMomo)
                AddChartIndicator(Momentum1);

            VMA1 = VMA(Close, 9, 9);
            VMA1.Plots[0].Brush = Brushes.SkyBlue;
            VMA1.Plots[0].Width = 3;
            if (showVMA)
                AddChartIndicator(VMA1);

            ADX1 = ADX(Close, adxPeriod);
            ADX1.Plots[0].Brush = Brushes.Cyan;
            ADX1.Plots[0].Width = 2;
            if (showAdx)
                AddChartIndicator(ADX1);

            ATR1 = ATR(AtrPeriod);

            maxProfit = totalPnL;
        }

        private void LoadChartTraderButtons()
        {
            Dispatcher.InvokeAsync(() =>
            {
                CreateWPFControls();
            });
        }

        private void CleanUpStrategy()
        {
            ChartControl?.Dispatcher.InvokeAsync(() =>
            {
                DisposeWPFControls();
            });

            //			clientWebSocket?.Dispose();

            lock (orderLock)
            {
                if (activeOrders.Count > 0)
                {
                    Print($"{Time[0]}: Strategy terminated with active orders. Investigate:");
                    foreach (var kvp in activeOrders)
                    {
                        Print($"{Time[0]}: Order Label: {kvp.Key}, Order ID: {kvp.Value.OrderId}");
                        CancelOrder(kvp.Value);
                    }
                }
            }
        }

        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            try
            {
                if (BarsInProgress != 0 || CurrentBars[0] < BarsRequiredToTrade || orderErrorOccurred)
                    return;

                if (Bars.IsFirstBarOfSession)
                {
                    canTradeToday = true;
                    cumPnL = totalPnL;
                    ///Double that copies the full session PnL (If trading multiple days). Is only calculated once per day.
                    dailyPnL = totalPnL - cumPnL;
                    ///Subtract the copy of the full session by the full session PnL. This resets your daily PnL back to 0.
                    Print(
                        $"{Time[0]} //On Bar Update//// IsFirst Bar of SessioncumPnL: {cumPnL}, dailyPnL: {dailyPnL}, totalPnL: {totalPnL}, CumProfit is {SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit}"
                    );
                }

                if (!canTradeToday || State == State.Historical)
                    return;

                //Track the Highest Profit Achieved
                if (totalPnL > maxProfit)
                {
                    maxProfit = totalPnL;
                    Print($"{Time[0]} ///On Bar Update//// Updated maxProfit: {maxProfit}");
                }

                dailyPnL = (totalPnL) - (cumPnL);
                ///Your daily limit is the difference between these

                // Account Reconciliation
                if (DateTime.Now - lastAccountReconciliationTime > accountReconciliationInterval)
                {
                    ReconcileAccountOrders();
                    lastAccountReconciliationTime = DateTime.Now;
                }

                regChanUp = RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1];
                regChanDown = RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1];

                buyPressure[0] = BuySellPressure1.BuyPressure[0];
                sellPressure[0] = BuySellPressure1.SellPressure[0];

                buyPressureUp =
                    !enableBuySellPressure
                    || (BuySellPressure1.BuyPressure[0] > BuySellPressure1.SellPressure[0]);
                sellPressureUp =
                    !enableBuySellPressure
                    || (BuySellPressure1.SellPressure[0] > BuySellPressure1.BuyPressure[0]);

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

                momentum = Momentum1[0];
                momoUp = !enableMomo || (Momentum1[0] > MomoUp && Momentum1[0] > Momentum1[1]);
                momoDown = !enableMomo || (Momentum1[0] < MomoDown && Momentum1[0] < Momentum1[1]);

                currentAdx = ADX1[0];
                adxUp = !enableADX || ADX1[0] > adxThreshold && ADX1[0] < adxThreshold2;

                atrUp = !enableVolatility || ATR1[0] > atrThreshold;

                if (EnableChoppinessDetection)
                {
                    // --- Regression Channel Slope Choppiness ---
                    marketIsChoppy = false; // Default

                    // Check if enough bars exist for slope calculation AND for ADX
                    if (
                        CurrentBar
                        >= Math.Max(RegChanPeriod, Math.Max(adxPeriod, SlopeLookBack)) - 1
                    )
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
                        TransparentColor(128, Colors.LightGray);
                    }
                    else
                    {
                        // Reset the background when the condition is false
                        BackBrush = null; // Or Brushes.Transparent, or your default chart background if known
                    }

                    if (marketIsChoppy)
                    {
                        if (isAutoTradeEnabled) // Only act if it was enabled
                        {
                            isAutoTradeEnabled = false;
                            autoDisabledByChop = true; // Mark that the *system* disabled it
                            autoStatusChanged = true;
                            Print(
                                $"{Time[0]}: Market choppy (ADX={currentAdx:F1} < {ChopAdxThreshold}, BBWidth Factor < {FlatSlopeFactor:P0}). Auto trading DISABLED."
                            );
                        }
                    }
                    else // Market is NOT choppy
                    {
                        if (autoDisabledByChop) // Only re-enable if *system* disabled it
                        {
                            isAutoTradeEnabled = true;
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
                        ChartControl.Dispatcher.InvokeAsync(() =>
                        { // Ensures UI update happens on UI thread
                            if (isAutoTradeEnabled)
                            {
                                DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
                                DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");
                            }
                            else
                            {
                                DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
                                DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");
                            }
                        });
                    }
                }

                uptrend =
                    momoUp && buyPressureUp && hmaUp && volMaUp && regChanUp && adxUp && atrUp;
                downtrend =
                    momoDown
                    && sellPressureUp
                    && hmaDown
                    && volMaDown
                    && regChanDown
                    && adxUp
                    && atrUp;

                priceUp = Close[0] > Close[1] && Close[0] > Open[0];
                priceDown = Close[0] < Close[1] && Close[0] < Open[0];

                entryPrice = Position.AveragePrice;
                currentPrice = Close[0];

			// ─────────────────────────────
		    // Evaluate Signals from all additional (multi) data series
		    // ─────────────────────────────
			EvaluateMultiTimeSeriesSignals();
			
			// Now combine the multi-series signals into the entry condition.
	        // For example, you might require that at least MinRequiredTimeSeriesSignals count are true:
	        int countExtraSignals = multiSeriesSignals.Values.Count(s => s);
        
			// Determine if additional series conditions are valid.
			bool extraSeriesValid = enabledTimeframes.Count == 0 
			    ? true 
			    : multiSeriesSignals.Values.Count(s => s) >= MinRequiredTimeSeriesSignals;
			
                UpdatePositionState();

                if (Bars.IsFirstBarOfSession)
                {
                    cumPnL = totalPnL;
                    dailyPnL = totalPnL - cumPnL;
                }

                if (showPnl)
                    ShowPNLStatus();

                if (isAutoTradeEnabled)
                {
                    ProcessLongEntry();
                    ProcessShortEntry();
                }

                if (enableBackgroundSignal)
                {
                    if (uptrend)
                    {
                        TransparentColor(32, Colors.Lime);
                    }
                    else if (downtrend)
                    {
                        TransparentColor(32, Colors.Crimson);
                    }
                    else
                    {
                        // Reset the background when the condition is false
                        BackBrush = null; // Or Brushes.Transparent, or your default chart background if known
                    }
                }

                if (showDailyPnl)
                    DrawStrategyPnL();

                if (!isAtmStrategyCreated)
                    return;

                UpdateAtmStrategyStatus();

                if (atmStrategyId.Length > 0)
                {
                    //					UpdateStopPrice();
                    PrintAtmStrategyInfo();
                }

                ResetTradesPerDirection();
                ResetButtons();
                KillSwitch();
            }
            catch (Exception e)
            {
                Print(
                    $"{Time[0]} CRITICAL ERROR in OnBarUpdate: {e.Message} --- StackTrace: {e.StackTrace}"
                );
                orderErrorOccurred = true; // Stop further processing
                // Consider adding SetState(State.Terminated); here if you want it to fully stop on any error
                // SetState(State.Terminated);
            }
        }

        #endregion

        #region Transparent Background Color
        private void TransparentColor(byte alpha, Color baseColor)
        {
            // alpha = transparency, 50% = 128
            // Create the new semi-transparent color
            Color semiTransparentColor = Color.FromArgb(
                alpha,
                baseColor.R,
                baseColor.G,
                baseColor.B
            );
            // Create the new brush
            SolidColorBrush semiTransparentBrush = new SolidColorBrush(semiTransparentColor);
            // Freeze the brush for performance (important!)
            semiTransparentBrush.Freeze();
            // Assign the semi-transparent brush to BackBrush
            BackBrush = semiTransparentBrush;
        }
        #endregion

        private void UpdatePositionState()
        {
            isLong = Position.MarketPosition == MarketPosition.Long;
            isShort = Position.MarketPosition == MarketPosition.Short;
            isFlat = Position.MarketPosition == MarketPosition.Flat;
        }

        private bool AtmStrategyNotActive()
        {
            return orderId.Length == 0 && atmStrategyId.Length == 0;
        }

        // Updated ProcessLongEntry method
        // This method now checks for the extra series signals before placing a long entry.
        private void ProcessLongEntry()
        {
            if (IsLongEntryConditionMet())
            {
                if (!tradesPerDirection || (tradesPerDirection && counterLong < longPerDirection))
                {
                    counterLong++;
                    counterShort = 0;

                    CreateAtmStrategy(OrderAction.Buy, LongEntryLabel, Brushes.Cyan);
                }
                else
                {
                    Print("Limit long trades in a row");
                }
            }
        }

        // Updated ProcessShortEntry method
        // This method now checks for the extra series signals before placing a short entry.
        private void ProcessShortEntry()
        {
            if (IsShortEntryConditionMet())
            {
                if (!tradesPerDirection || (tradesPerDirection && counterShort < shortPerDirection))
                {
                    counterLong = 0;
                    counterShort++;

                    CreateAtmStrategy(OrderAction.SellShort, ShortEntryLabel, Brushes.Yellow);
                }
                else
                {
                    Print("Limit short trades in a row");
                }
            }
        }

        private bool IsLongEntryConditionMet()
        {
            return longSignal
                && AtmStrategyNotActive()
                && (isLongEnabled)
                && extraSeriesValid // new parameter for extra data series signals
                && (checkTimers())
                && ((dailyLossProfit ? dailyPnL > -DailyLossLimit : true))
                && ((dailyLossProfit ? dailyPnL < DailyProfitLimit : true))
                && (isFlat)
                && (uptrend)
                && (priceUp)
                && (!trailingDrawdownReached)
                && (canTradeOK)
                && (canTradeToday);
        }

        private bool IsShortEntryConditionMet()
        {
            return shortSignal
                && AtmStrategyNotActive()
                && (isShortEnabled)
                && extraSeriesValid // new parameter for extra data series signals
                && (checkTimers())
                && ((dailyLossProfit ? dailyPnL > -DailyLossLimit : true))
                && ((dailyLossProfit ? dailyPnL < DailyProfitLimit : true))
                && (isFlat)
                && (downtrend)
                && (priceDown)
                && (!trailingDrawdownReached)
                && (canTradeOK)
                && (canTradeToday);
        }

		#region EvaluateMultiTimeSeriesSignals
	    // This method loops through each enabled additional data series and evaluates a sample condition.
	    // In this example, we simply check whether the close of the additional series is greater than its open.
	    // You can replace this with any custom condition per your strategy.
	    private void EvaluateMultiTimeSeriesSignals()
	    {
	        // Clear previous signals
	        multiSeriesSignals.Clear();
	
	        foreach (int idx in enabledTimeframes)
	        {
	            // Make sure there are enough bars in the additional series.
	            if (BarsArray[idx].Count > 1)
	            {
	                // Access the additional data series using its BarsArray index.
	                double seriesClose = BarsArray[idx].GetClose(BarsArray[idx].Count - 1);
	                double seriesOpen = BarsArray[idx].GetOpen(BarsArray[idx].Count - 1);
	
	                // Example condition: signal is true if close > open.
	                bool seriesSignal = seriesClose > seriesOpen;
	                multiSeriesSignals[idx] = seriesSignal;
	            }
	            else
	            {
	                // Not enough data – assume no signal.
	                multiSeriesSignals[idx] = false;
	            }
	        }
	    }
	    #endregion

        private void ResetTradesPerDirection()
        {
            if (tradesPerDirection)
            {
                if (counterLong != 0 && Close[1] < Open[1])
                    counterLong = 0;
                if (counterShort != 0 && Close[1] > Open[1])
                    counterShort = 0;
            }
        }

        private void ResetButtons()
        {
            if (isFlat)
            {
                quickLongBtnActive = false;
                quickShortBtnActive = false;

                lock (orderLock)
                {
                    activeOrders.Clear();
                }
            }
        }

        #region ATM Strategy Methods

        private void CreateAtmStrategy(
            OrderAction orderAction,
            string signalName,
            Brush signalBrush
        )
        {
            isAtmStrategyCreated = false;
            atmStrategyId = GetAtmStrategyUniqueId();
            orderId = GetAtmStrategyUniqueId();

            Print($"Your atmStrategyId is: {atmStrategyId} OrderId is: {orderId}");

            //			OrderType orderType = (OrderType == OrderType.Market) ? OrderType.Market : OrderType.Limit;
            double orderPrice =
                (orderType == OrderType.Limit)
                    ? (
                        orderAction == OrderAction.Buy
                            ? GetCurrentBid() - LimitOffset * TickSize
                            : GetCurrentAsk() + LimitOffset * TickSize
                    )
                    : 0;

            AtmStrategyCreate(
                orderAction,
                orderType,
                orderPrice,
                0,
                TimeInForce.Gtc,
                orderId,
                ATMStrategyTemplate,
                atmStrategyId,
                (atmCallbackErrorCode, atmCallBackId) =>
                {
                    if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                        isAtmStrategyCreated = true;
                }
            );

            DrawArrow(signalName, orderPrice, signalBrush);
        }

        private void DrawArrow(string signalName, double signalPrice, Brush signalBrush)
        {
            if (signalName == LongEntryLabel)
                Draw.ArrowUp(this, signalName + CurrentBars[0], false, 0, signalPrice, signalBrush);
            else if (signalName == ShortEntryLabel)
                Draw.ArrowDown(
                    this,
                    signalName + CurrentBars[0],
                    false,
                    0,
                    signalPrice,
                    signalBrush
                );
        }

        private void UpdateAtmStrategyStatus()
        {
            if (orderId.Length > 0)
            {
                string[] status = GetAtmStrategyEntryOrderStatus(orderId);

                if (status.Length > 0)
                {
                    PrintOrderStatus(status);
                    if (
                        status[2] == "Filled"
                        || status[2] == "Cancelled"
                        || status[2] == "Rejected"
                    )
                        orderId = string.Empty;
                }
            }
            else if (
                atmStrategyId.Length > 0
                && GetAtmStrategyMarketPosition(atmStrategyId) == MarketPosition.Flat
            )
            {
                atmStrategyId = string.Empty;
            }
        }

        private void PrintOrderStatus(string[] status)
        {
            Print($"The entry order average fill price is: {status[0]}");
            Print($"The entry order filled amount is: {status[1]}");
            Print($"The entry order order state is: {status[2]}");
        }

        private void PrintAtmStrategyInfo()
        {
            Print(
                $"The current ATM Strategy market position is: {GetAtmStrategyMarketPosition(atmStrategyId)}"
            );
            Print(
                $"The current ATM Strategy position quantity is: {GetAtmStrategyPositionQuantity(atmStrategyId)}"
            );
            Print(
                $"The current ATM Strategy average price is: {GetAtmStrategyPositionAveragePrice(atmStrategyId)}"
            );
            Print(
                $"The current ATM Strategy Unrealized PnL is: {GetAtmStrategyUnrealizedProfitLoss(atmStrategyId)}"
            );
        }
        #endregion

        #region Rogue Order Detection

        private void ReconcileAccountOrders()
        {
            lock (orderLock)
            {
                try
                {
                    var accounts = Account.All;

                    if (accounts == null || accounts.Count == 0)
                    {
                        Print($"{Time[0]}: No accounts found.");
                        return;
                    }

                    foreach (Account account in accounts)
                    {
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
                            Print(
                                $"{Time[0]}: Error getting orders for account {account.Name}: {ex.Message}"
                            );
                            continue;
                        }

                        if (accountOrders == null || accountOrders.Count == 0)
                        {
                            Print($"{Time[0]}: No orders found in account {account.Name}.");
                            continue;
                        }

                        HashSet<string> strategyOrderIds = new HashSet<string>(
                            activeOrders.Values.Select(o => o.OrderId)
                        );

                        foreach (Order accountOrder in accountOrders)
                        {
                            if (!strategyOrderIds.Contains(accountOrder.OrderId))
                            {
                                Print(
                                    $"{Time[0]}: Rogue order detected! Account: {accountOrder.Account.Name} OrderId: {accountOrder.OrderId}, OrderType: {accountOrder.OrderType}, OrderStatus: {accountOrder.OrderState}, Quantity: {accountOrder.Quantity}, AveragePrice: {accountOrder.AverageFillPrice}"
                                );

                                try
                                {
                                    CancelOrder(accountOrder);
                                    Print(
                                        $"{Time[0]}: Attempted to cancel rogue order: {accountOrder.OrderId}"
                                    );
                                }
                                catch (Exception ex)
                                {
                                    Print(
                                        $"{Time[0]}: Failed to Cancel rogue order. Account: {accountOrder.Account.Name} OrderId: {accountOrder.OrderId}, OrderType: {accountOrder.OrderType}, OrderStatus: {accountOrder.OrderState}, Quantity: {accountOrder.Quantity}, AveragePrice: {accountOrder.AverageFillPrice}, Reason: {ex.Message}"
                                    );
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"{Time[0]}: Error during account reconciliation: {ex.Message}");
                    orderErrorOccurred = true;
                }
            }
        }

        #endregion

        // Method to check the minimum interval between order submissions
        private bool CanSubmitOrder()
        {
            return (DateTime.Now - lastOrderActionTime) >= minOrderActionInterval;
        }

        #region OnExecutionUpdate

        protected virtual void OnExecutionUpdate(
            Execution execution,
            string executionId,
            double price,
            int quantity,
            MarketPosition marketPosition,
            string orderId,
            DateTime time
        )
        {
            // *** CRITICAL: Track order fills, modifications, and cancellations ***
            lock (orderLock)
            {
                // Find the order in our activeOrders dictionary
                string orderLabel = activeOrders
                    .FirstOrDefault(x => x.Value.OrderId == orderId)
                    .Key;

                if (!string.IsNullOrEmpty(orderLabel))
                {
                    switch (execution.Order.OrderState)
                    {
                        case OrderState.Filled:
                            Print($"{Time[0]}: Order {orderId} with label {orderLabel} filled.");
                            activeOrders.Remove(orderLabel); // Remove the order when it's filled.

                            if (execution.Order.OrderState == OrderState.Filled && isFlat)
                            {
                                if (
                                    execution.Order.Name.StartsWith("LE")
                                    || execution.Order.Name.StartsWith("QLE")
                                    || execution.Order.Name.StartsWith("Add1LE")
                                )
                                {
                                    counterLong = 0;
                                }
                                else if (
                                    execution.Order.Name.StartsWith("SE")
                                    || execution.Order.Name.StartsWith("QSE")
                                    || execution.Order.Name.StartsWith("Add1SE")
                                )
                                {
                                    counterShort = 0;
                                }
                            }

                            break;

                        case OrderState.Cancelled:
                            Print($"{Time[0]}: Order {orderId} with label {orderLabel} cancelled.");
                            activeOrders.Remove(orderLabel); // Remove cancelled orders
                            break;

                        case OrderState.Rejected:
                            Print($"{Time[0]}: Order {orderId} with label {orderLabel} rejected.");
                            activeOrders.Remove(orderLabel); // Remove rejected orders
                            break;

                        default:
                            Print(
                                $"{Time[0]}: Order {orderId} with label {orderLabel} updated to state: {execution.Order.OrderState}"
                            );
                            break;
                    }
                }
                else
                {
                    // This could indicate a rogue order or an order not tracked by the strategy.
                    Print(
                        $"{Time[0]}: Execution update for order {orderId}, but order is not tracked by the strategy."
                    );

                    // Attempt to Cancel the Rogue Order
                    try
                    {
                        CancelOrder(execution.Order);
                        Print($"{Time[0]}: Successfully Canceled the Rogue Order: {orderId}.");
                    }
                    catch (Exception ex)
                    {
                        Print(
                            $"{Time[0]}: Could not Cancel the Rogue Order: {orderId}. {ex.Message}"
                        );
                        orderErrorOccurred = true; // Consider whether to halt trading
                    }
                }
            }
        }

        #endregion

        #region Daily PNL

        protected override void OnPositionUpdate(
            Cbi.Position position,
            double averagePrice,
            int quantity,
            Cbi.MarketPosition marketPosition
        )
        {
            if (isFlat && SystemPerformance.AllTrades.Count > 0)
            {
                //				PositionPnl = TextPosition.BottomLeft;
                //				totalPnL = 0; //backtest

                totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
                ///Double that sets the total PnL
                dailyPnL = (totalPnL) - (cumPnL);
                ///Your daily limit is the difference between these

                // Re-enable the strategy if it was disabled by the DD and totalPnL increases
                if (
                    enableTrailingDD
                    && trailingDrawdownReached
                    && totalPnL > maxProfit - TrailingDrawdown
                )
                {
                    trailingDrawdownReached = false;
                    isManualTradeEnabled = true;
                    Print("Trailing Drawdown Lifted. Strategy Re-Enabled!");
                }

                if (dailyPnL <= -DailyLossLimit) //Print this when daily Pnl is under Loss Limit
                {
                    Print(
                        "Daily Loss of "
                            + DailyLossLimit
                            + " has been hit. No More Entries! Daily PnL >> "
                            + dailyPnL
                            + " <<"
                            + Time[0]
                    );

                    Text myTextLoss = Draw.TextFixed(
                        this,
                        "loss_text",
                        "Daily Loss of "
                            + DailyLossLimit
                            + " has been hit. No More Entries! Daily PnL >> "
                            + "$"
                            + totalPnL
                            + " <<",
                        PositionDailyPNL,
                        colorDailyProfitLoss,
                        ChartControl.Properties.LabelFont,
                        Brushes.Transparent,
                        Brushes.Transparent,
                        100
                    );
                    myTextLoss.Font = new SimpleFont("Arial", 16) { Bold = true };
                }

                if (dailyPnL >= DailyProfitLimit) //Print this when daily Pnl is above Profit limit
                {
                    Print(
                        "Daily Profit of "
                            + DailyProfitLimit
                            + " has been hit. No more Entries! Daily PnL >>"
                            + dailyPnL
                            + " <<"
                            + Time[0]
                    );

                    Text myTextProfit = Draw.TextFixed(
                        this,
                        "profit_text",
                        "Daily Profit of "
                            + DailyProfitLimit
                            + " has been hit. No more Entries! Daily PnL >>"
                            + "$"
                            + totalPnL
                            + " <<",
                        PositionDailyPNL,
                        colorDailyProfitLoss,
                        ChartControl.Properties.LabelFont,
                        Brushes.Transparent,
                        Brushes.Transparent,
                        100
                    );
                    myTextProfit.Font = new SimpleFont("Arial", 18) { Bold = true };
                }
            }

            if (isFlat)
                checkPositions(); // Detect unwanted Positions opened (possible rogue Order?)
        }

        protected void checkPositions()
        {
            //	Detect unwanted Positions opened (possible rogue Order?)
            double currentPosition = Position.Quantity; // Get current position quantity

            if (isFlat)
            {
                foreach (var order in Orders)
                {
                    if (order != null)
                        CancelOrder(order);
                }
            }
        }

        #endregion

        #region Chart Trader Button Handling
        protected void DecorateButton(
            Button button,
            string content,
            Brush background,
            Brush borderBrush,
            Brush foreground
        )
        {
            button.Content = content;
            button.Background = background;
            button.BorderBrush = borderBrush;
            button.Foreground = foreground;
        }

        protected void DecorateDisabledButtons(Button myButton, string stringButton)
        {
            DecorateButton(myButton, stringButton, Brushes.DarkRed, Brushes.Black, Brushes.White);
        }

        protected void DecorateEnabledButtons(Button myButton, string stringButton)
        {
            DecorateButton(myButton, stringButton, Brushes.DarkGreen, Brushes.Black, Brushes.White);
        }

        protected void DecorateNeutralButtons(Button myButton, string stringButton)
        {
            DecorateButton(myButton, stringButton, Brushes.LightGray, Brushes.Black, Brushes.Black);
        }

        protected void DecorateGrayButtons(Button myButton, string stringButton)
        {
            DecorateButton(myButton, stringButton, Brushes.DarkGray, Brushes.Black, Brushes.Black);
        }

        protected void CreateWPFControls()
        {
            chartWindow = System.Windows.Window.GetWindow(ChartControl.Parent) as Chart;

            if (chartWindow == null)
                return;

            chartTraderGrid =
                (
                    chartWindow.FindFirst("ChartWindowChartTraderControl") as Gui.Chart.ChartTrader
                ).Content as Grid;
            chartTraderButtonsGrid = chartTraderGrid.Children[0] as Grid;

            InitializeButtonGrid(); // Call InitializeButtonGrid FIRST
            CreateButtons(); // Call CreateButtons BEFORE SetButtonLocations and AddButtonsToPanel

            addedRow = new RowDefinition() { Height = new GridLength(250) };

            SetButtonLocations();
            AddButtonsToPanel();

            if (TabSelected())
                InsertWPFControls();

            chartWindow.MainTabControl.SelectionChanged += TabChangedHandler;
        }

        protected void CreateButtons()
        {
            Style basicButtonStyle =
                System.Windows.Application.Current.FindResource("BasicEntryButton") as Style;

            manualBtn = CreateButton(
                "\uD83D\uDD12 Manual On",
                basicButtonStyle,
                "Enable (Green) / Disbled (Red) Manual Button",
                OnButtonClick
            );
            if (isManualTradeEnabled)
                DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
            else
                DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");

            autoBtn = CreateButton(
                "\uD83D\uDD12 Auto On",
                basicButtonStyle,
                "Enable (Green) / Disbled (Red) Auto Button",
                OnButtonClick
            );
            if (isAutoTradeEnabled)
                DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
            else
                DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");

            longBtn = CreateButton(
                "LONG",
                basicButtonStyle,
                "Enable (Green) / Disbled (Red) Auto Long Entry",
                OnButtonClick
            );
            if (isLongEnabled)
                DecorateEnabledButtons(longBtn, "LONG");
            else
                DecorateDisabledButtons(longBtn, "LONG Off");

            shortBtn = CreateButton(
                "SHORT",
                basicButtonStyle,
                "Enable (Green) / Disbled (Red) Auto Short Entry",
                OnButtonClick
            );
            if (isShortEnabled)
                DecorateEnabledButtons(shortBtn, "SHORT");
            else
                DecorateDisabledButtons(shortBtn, "SHORT Off");

            quickLongBtn = CreateButton("Buy", basicButtonStyle, "Buy Market Entry", OnButtonClick);
            DecorateEnabledButtons(quickLongBtn, "Buy");

            quickShortBtn = CreateButton(
                "Sell",
                basicButtonStyle,
                "Sell Market Entry",
                OnButtonClick
            );
            DecorateDisabledButtons(quickShortBtn, "Sell");

            closeBtn = CreateButton(
                "Close All Positions",
                basicButtonStyle,
                "Manual Close: CloseAllPosiions manually",
                OnButtonClick,
                Brushes.DarkRed,
                Brushes.White
            );
            panicBtn = CreateButton(
                "\u2620 Panic Shutdown",
                basicButtonStyle,
                "PanicBtn: CloseAllPosiions",
                OnButtonClick,
                Brushes.DarkRed,
                Brushes.Yellow
            );

            paypalBtn = CreateButton(
                "Donate (PayPal)",
                basicButtonStyle,
                "paypalBtn: Donate",
                OnButtonClick,
                Brushes.DarkBlue,
                Brushes.Yellow
            );
        }

        private Button CreateButton(
            string content,
            Style style,
            string toolTip,
            RoutedEventHandler clickHandler,
            Brush background = null,
            Brush foreground = null
        )
        {
            Button button = new Button
            {
                Content = content,
                Height = 25,
                Margin = new Thickness(1, 0, 1, 0),
                Padding = new Thickness(0, 0, 0, 0),
                Style = style,
                BorderThickness = new Thickness(1.5),
                IsEnabled = true,
                ToolTip = toolTip,
            };

            if (background != null)
                button.Background = background;
            if (foreground != null)
                button.Foreground = foreground;

            button.Click += clickHandler;

            return button;
        }

        protected void InitializeButtonGrid()
        {
            lowerButtonsGrid = new Grid();

            for (int i = 0; i < 2; i++)
            {
                lowerButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int i = 0; i <= 9; i++)
            {
                lowerButtonsGrid.RowDefinitions.Add(new RowDefinition());
            }
        }

        protected void SetButtonLocations()
        {
            SetButtonLocation(manualBtn, 0, 1);
            SetButtonLocation(autoBtn, 1, 1);
            SetButtonLocation(longBtn, 0, 2);
            SetButtonLocation(shortBtn, 1, 2);
            SetButtonLocation(quickLongBtn, 0, 3);
            SetButtonLocation(quickShortBtn, 1, 3);
            SetButtonLocation(closeBtn, 0, 4, 2);
            SetButtonLocation(panicBtn, 0, 5, 2);
            SetButtonLocation(paypalBtn, 0, 6, 2);
        }

        protected void SetButtonLocation(Button button, int column, int row, int columnSpan = 1)
        {
            Grid.SetColumn(button, column);
            Grid.SetRow(button, row);

            if (columnSpan > 1)
                Grid.SetColumnSpan(button, columnSpan);
        }

        protected void AddButtonsToPanel()
        {
            lowerButtonsGrid.Children.Add(manualBtn);
            lowerButtonsGrid.Children.Add(autoBtn);
            lowerButtonsGrid.Children.Add(longBtn);
            lowerButtonsGrid.Children.Add(shortBtn);
            lowerButtonsGrid.Children.Add(quickLongBtn);
            lowerButtonsGrid.Children.Add(quickShortBtn);
            lowerButtonsGrid.Children.Add(closeBtn);
            lowerButtonsGrid.Children.Add(panicBtn);
            lowerButtonsGrid.Children.Add(paypalBtn);
        }

        protected void OnButtonClick(object sender, RoutedEventArgs rea)
        {
            Button button = sender as Button;

            if (button == manualBtn)
            {
                isManualTradeEnabled = !isManualTradeEnabled;
                if (isManualTradeEnabled)
                {
                    DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
                    DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");
                }
                else
                {
                    DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");
                    DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
                }
                Print($"Strategy: {isManualTradeEnabled}");
                return;
            }

            if (button == autoBtn)
            {
                isAutoTradeEnabled = !isAutoTradeEnabled;
                if (isAutoTradeEnabled)
                {
                    DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
                    DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");
                }
                else
                {
                    DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");
                    DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
                }
                Print($"Strategy: {isAutoTradeEnabled}");
                return;
            }

            if (button == longBtn)
            {
                isLongEnabled = !isLongEnabled;
                if (isLongEnabled)
                    DecorateEnabledButtons(longBtn, "LONG");
                else
                    DecorateDisabledButtons(longBtn, "LONG Off");
                Print($"Long Enabled: {isLongEnabled}");
                return;
            }

            if (button == shortBtn)
            {
                isShortEnabled = !isShortEnabled;
                if (isShortEnabled)
                    DecorateEnabledButtons(shortBtn, "SHORT");
                else
                    DecorateDisabledButtons(shortBtn, "SHORT Off");
                Print($"Short Enabled: {isShortEnabled}");
                return;
            }

            if (button == quickLongBtn && isManualTradeEnabled)
            {
                QuickLong = !QuickLong;
                Print($"Buy Market On: {QuickLong}");
                quickLongBtnActive = true;

                ProcessLongEntry();

                QuickLong = false;
                return;
            }

            if (button == quickShortBtn && isManualTradeEnabled)
            {
                QuickShort = !QuickShort;
                Print($"Sell Market On: {QuickShort}");
                quickShortBtnActive = true;

                ProcessShortEntry();

                QuickShort = false;
                return;
            }

            if (button == closeBtn)
            {
                CloseAllPositions();
                ForceRefresh();
                return;
            }
            if (button == panicBtn)
            {
                FlattenAllPositions();
                ForceRefresh();
                return;
            }

            if (button == paypalBtn)
            {
                System.Diagnostics.Process.Start(paypal);
                return;
            }
        }

        #region Dispose
        protected void DisposeWPFControls()
        {
            if (chartWindow != null)
                chartWindow.MainTabControl.SelectionChanged -= TabChangedHandler;

            manualBtn.Click -= OnButtonClick;
            autoBtn.Click -= OnButtonClick;
            longBtn.Click -= OnButtonClick;
            shortBtn.Click -= OnButtonClick;
            quickLongBtn.Click -= OnButtonClick;
            quickShortBtn.Click -= OnButtonClick;
            closeBtn.Click -= OnButtonClick;
            panicBtn.Click -= OnButtonClick;
            paypalBtn.Click -= OnButtonClick;

            RemoveWPFControls();
        }
        #endregion

        #region Insert WPF
        public void InsertWPFControls()
        {
            if (panelActive)
                return;

            chartTraderGrid.RowDefinitions.Add(addedRow);
            Grid.SetRow(lowerButtonsGrid, (chartTraderGrid.RowDefinitions.Count - 1));
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

        #region Tab Selected
        protected bool TabSelected()
        {
            foreach (TabItem tab in chartWindow.MainTabControl.Items)
                if (
                    (tab.Content as ChartTab).ChartControl == ChartControl
                    && tab == chartWindow.MainTabControl.SelectedItem
                )
                    return true;

            return false;
        }

        protected void TabChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count <= 0)
                return;

            tabItem = e.AddedItems[0] as TabItem;
            if (tabItem == null)
                return;

            chartTab = tabItem.Content as ChartTab;
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
            if (!string.IsNullOrEmpty(atmStrategyId))
            {
                Print("Closing open position for ATM strategy.");
                AtmStrategyClose(atmStrategyId);
            }
            else
            {
                Print("No active ATM strategy to close.");
            }
        }

        protected void FlattenAllPositions()
        {
            System.Collections.ObjectModel.Collection<Cbi.Instrument> instrumentsToClose =
                new System.Collections.ObjectModel.Collection<Instrument>();
            instrumentsToClose.Add(Position.Instrument);
            Position.Account.Flatten(instrumentsToClose);
        }

        #endregion

        protected bool checkTimers()
        {
            if (
                (Times[0][0].TimeOfDay >= Start.TimeOfDay)
                    && (Times[0][0].TimeOfDay < End.TimeOfDay)
                || (
                    isEnableTime2
                    && Times[0][0].TimeOfDay >= Start2.TimeOfDay
                    && Times[0][0].TimeOfDay <= End2.TimeOfDay
                )
                || (
                    isEnableTime3
                    && Times[0][0].TimeOfDay >= Start3.TimeOfDay
                    && Times[0][0].TimeOfDay <= End3.TimeOfDay
                )
                || (
                    isEnableTime4
                    && Times[0][0].TimeOfDay >= Start4.TimeOfDay
                    && Times[0][0].TimeOfDay <= End4.TimeOfDay
                )
                || (
                    isEnableTime5
                    && Times[0][0].TimeOfDay >= Start5.TimeOfDay
                    && Times[0][0].TimeOfDay <= End5.TimeOfDay
                )
                || (
                    isEnableTime6
                    && Times[0][0].TimeOfDay >= Start6.TimeOfDay
                    && Times[0][0].TimeOfDay <= End6.TimeOfDay
                )
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private string GetActiveTimer()
        {
            //	check active timer
            TimeSpan currentTime = Times[0][0].TimeOfDay;

            if (
                (Times[0][0].TimeOfDay >= Start.TimeOfDay)
                && (Times[0][0].TimeOfDay < End.TimeOfDay)
            )
            {
                return $"{Start:HH\\:mm} - {End:HH\\:mm}";
            }
            else if (
                Time2
                && Times[0][0].TimeOfDay >= Start2.TimeOfDay
                && Times[0][0].TimeOfDay <= End2.TimeOfDay
            )
            {
                return $"{Start2:HH\\:mm} - {End2:HH\\:mm}";
            }
            else if (
                Time3
                && Times[0][0].TimeOfDay >= Start3.TimeOfDay
                && Times[0][0].TimeOfDay <= End3.TimeOfDay
            )
            {
                return $"{Start3:HH\\:mm} - {End3:HH\\:mm}";
            }
            else if (
                Time4
                && Times[0][0].TimeOfDay >= Start4.TimeOfDay
                && Times[0][0].TimeOfDay <= End4.TimeOfDay
            )
            {
                return $"{Start4:HH\\:mm} - {End4:HH\\:mm}";
            }
            else if (
                Time5
                && Times[0][0].TimeOfDay >= Start5.TimeOfDay
                && Times[0][0].TimeOfDay <= End5.TimeOfDay
            )
            {
                return $"{Start5:HH\\:mm} - {End5:HH\\:mm}";
            }
            else if (
                Time6
                && Times[0][0].TimeOfDay >= Start6.TimeOfDay
                && Times[0][0].TimeOfDay <= End6.TimeOfDay
            )
            {
                return $"{Start6:HH\\:mm} - {End6:HH\\:mm}";
            }

            return "No active timer";
        }

        protected void ShowPNLStatus()
        {
            string textLine1 = GetActiveTimer();
            string textLine3 =
                $"{counterLong} / {longPerDirection} | " + (tradesPerDirection ? "On" : "Off");
            string textLine5 =
                $"{counterShort} / {shortPerDirection} | " + (tradesPerDirection ? "On" : "Off");

            string statusPnlText =
                $"Active Timer:\t{textLine1}\nLong Per Direction:\t{textLine3}\nShort Per Direction:\t{textLine5}";
            SimpleFont font = new SimpleFont("Arial", 16);

            Draw.TextFixed(
                this,
                "statusPnl",
                statusPnlText,
                PositionPnl,
                colorPnl,
                font,
                Brushes.Transparent,
                Brushes.Transparent,
                0
            );
        }

        // removed for now (MarketMath 4/8/25)
        //		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        //		{
        //			base.OnRender(chartControl, chartScale);
        //			if (showDailyPnl) DrawStrategyPnL(chartControl);
        //		}

        protected void DrawStrategyPnL()
        {
            if (Account == null)
                return; // Added safety check for connection

            // ... (Get account PnL logic remains the same) ...
            double accountRealized =
                (State == State.Realtime)
                    ? Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar)
                    : SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
            double accountUnrealized =
                (State == State.Realtime)
                    ? Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar)
                    : (
                        Position != null && Position.MarketPosition != MarketPosition.Flat
                            ? Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0])
                            : 0
                    ); // Safety for Position
            double accountTotal = accountRealized + accountUnrealized;
            dailyPnL = accountTotal - cumPnL; // Assuming cumPnL is correctly managed elsewhere
            if (accountTotal > maxProfit)
                maxProfit = accountTotal;

            // --- Determine Trend and Signal Strings ---
            // Need to ensure these conditions don't themselves cause issues if called too early
            // But typically OnBarUpdate handles the main indicator calculations first.
            string trendStatus =
                uptrend ? "Up"
                : downtrend ? "Down"
                : "Neutral";
            string signalStatus = "No Signal"; // Default

            // These checks rely on flags set in OnBarUpdate, which should be safe if OnBarUpdate handled warm-up
            if (IsLongEntryConditionMet())
                signalStatus = "Long Ready";
            else if (IsShortEntryConditionMet())
                signalStatus = "Short Ready";

            // --- Apply Overrides based on State ---
            if (!isFlat)
                signalStatus = "In Position";

            if (EnableChoppinessDetection)
            {
                if (!isAutoTradeEnabled && !autoDisabledByChop)
                    signalStatus = "Auto OFF (Manual)"; // User turned it off

                if (marketIsChoppy) // Choppiness override (applies even if Auto is OFF manually)
                {
                    trendStatus = "Choppy";
                    signalStatus = "No Trade (Chop)";
                }
                if (!isAutoTradeEnabled && autoDisabledByChop)
                    signalStatus = "Auto OFF (Chop)"; // System turned it off due to chop
            }

            // Other overrides (higher priority?)
            if (!checkTimers())
                signalStatus = "Outside Hours";
            if (orderErrorOccurred)
                signalStatus = "Order Error!";
            if (enableTrailingDD && trailingDrawdownReached)
                signalStatus = "DD Limit Hit";
            if (dailyLossProfit && dailyPnL <= -DailyLossLimit)
                signalStatus = "Loss Limit Hit";
            if (dailyLossProfit && dailyPnL >= DailyProfitLimit)
                signalStatus = "Profit Limit Hit";

            string pnlSource = (State == State.Realtime) ? "Account" : "System";
            // Added null check for Account.Connection
            string connectionStatus =
                (Account.Connection != null) ? Account.Connection.Status.ToString() : "N/A";

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
            string adxText = CurrentBar >= adxPeriod - 1 ? currentAdx.ToString("F1") : "N/A";
            string momoText = CurrentBar >= momentumPeriod - 1 ? momentum.ToString("F1") : "N/A";
            // Assuming buyPressure/sellPressure series are populated when BuySellPressure1 is calculated in OnBarUpdate
            string buyPressText =
                CurrentBar >= buySellPressureRequiredBars - 1
                    ? buyPressure[0].ToString("F1")
                    : "N/A";
            string sellPressText =
                CurrentBar >= buySellPressureRequiredBars - 1
                    ? sellPressure[0].ToString("F1")
                    : "N/A";
            // --- END FIXED INDICATOR VALUE DISPLAY ---


            string realTimeTradeText =
                $"{Account.Name} | {(Account.Connection != null ? Account.Connection.Options.Name : "N/A")} ({connectionStatus})\n"
                + $"PnL Src: {pnlSource}\n"
                + $"Real PnL:\t{accountRealized:C}\n"
                + $"Unreal PnL:\t{accountUnrealized:C}\n"
                + $"Total PnL:\t{accountTotal:C}\n"
                + $"Daily PnL:\t{dailyPnL:C}\n"
                + $"Max Profit:\t{(maxProfit == double.MinValue ? "N/A" : maxProfit.ToString("C"))}\n"
                + $"-------------\n"
                + $"ADX:\t\t{adxText}\n"
                + // Use safe text
                $"Momentum:\t{momoText}\n"
                + // Use safe text
                $"Buy Pressure:\t{buyPressText}\n"
                + // Use safe text
                $"Sell Pressure:\t{sellPressText}\n"
                + // Use safe text
                $"-------------\n"
                + $"Trend:\t{trendStatus}\n"
                + // Use overridden status
                $"Signal:\t{signalStatus}"; // Use overridden status

            SimpleFont font = new SimpleFont("Arial", 16);
            Brush pnlColor =
                accountTotal == 0 ? Brushes.Cyan
                : accountTotal > 0 ? Brushes.Lime
                : Brushes.Pink;

            try
            {
                // Ensure ChartControl and other UI elements are available before drawing
                //if (chartControl != null)
                //{
                Draw.TextFixed(
                    this,
                    "realTimeTradeText",
                    realTimeTradeText,
                    PositionDailyPNL,
                    pnlColor,
                    font,
                    Brushes.Transparent,
                    Brushes.Transparent,
                    0
                );
                //}
            }
            catch (Exception ex)
            {
                Print($"Error drawing PNL display: {ex.Message}");
            }
        }

        #endregion

        #region KillSwitch
        protected void KillSwitch()
        {
            totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
            dailyPnL = totalPnL + Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);

            // Determine all relevant order labels
            List<string> longOrderLabels = new List<string> { LongEntryLabel }; // Base Labels for Longs
            List<string> shortOrderLabels = new List<string> { ShortEntryLabel }; // Base Labels for Shorts

            // Common Action: Close all Positions and Disable the Strategy
            Action closePositionAndDisableStrategy = () =>
			{
			    if (!string.IsNullOrEmpty(atmStrategyId) && GetAtmStrategyMarketPosition(atmStrategyId) != MarketPosition.Flat)
			    {
			        Print($"{Time[0]} KillSwitch: Closing position via AtmStrategyClose for ID: {atmStrategyId}");
			        AtmStrategyClose(atmStrategyId);
			        atmStrategyId = string.Empty; // Clear the ID after closing
			        orderId = string.Empty;
			    }
			    else if (Position.MarketPosition != MarketPosition.Flat) // Fallback if no ATM ID or position exists outside ATM
			    {
			         Print($"{Time[0]} KillSwitch: Closing position via ExitLong/ExitShort (No active ATM ID found or position mismatch)");
			         if (Position.MarketPosition == MarketPosition.Long)
			             ExitLong(Convert.ToInt32(Position.Quantity), "LongExitKillSwitch", ""); // Empty label might be okay here
			         else if (Position.MarketPosition == MarketPosition.Short)
			             ExitShort(Convert.ToInt32(Position.Quantity), "ShortExitKillSwitch", "");
			    }
			
			    isAutoTradeEnabled = false; // Disable auto trading regardless
			    // Consider disabling manual too if it's a hard stop
			    // isManualTradeEnabled = false;
			    // Decorate buttons accordingly
			     ChartControl?.Dispatcher.InvokeAsync(() => {
			         // Update button states here
			     });
			    Print($"{Time[0]}: Kill Switch Activated: Strategy auto-trading DISABLED!");
			};

            if (dailyLossProfit && enableTrailingDD) //Check both the enableDailyLossLimit and enableTrailingDD
            {
                if (
                    totalPnL >= StartTrailingDD
                    && (maxProfit - totalPnL) >= TrailingDrawdown
                    && Position.Quantity > 0
                )
                {
                    closePositionAndDisableStrategy();
                    trailingDrawdownReached = true;
                    Print("Max drawdown has been reached!  No more trading for the day.");
                }
            }

            if (dailyLossProfit && enableTrailingDD) //Check both the enableDailyLossLimit and enableTrailingDD
            {
                if (totalPnL >= StartTrailingDD && (maxProfit - totalPnL) >= TrailingDrawdown)
                {
                    closePositionAndDisableStrategy();
                    trailingDrawdownReached = true;
                }
            }

            if (dailyPnL <= -DailyLossLimit)
            {
                closePositionAndDisableStrategy();
            }

            if (dailyPnL >= DailyProfitLimit)
            {
                closePositionAndDisableStrategy();
            }

            if (!isManualTradeEnabled)
                Print("Kill Switch Activated!");
        }
        #endregion

        #region Entry Signals & Inits

        protected abstract bool ValidateEntryLong();

        protected abstract bool ValidateEntryShort();

        protected virtual bool ValidateExitLong()
        {
            return false;
        }

        protected virtual bool ValidateExitShort()
        {
            return false;
        }

        protected abstract void InitializeIndicators();

        protected virtual void addDataSeries() { }

        #endregion

        #region Custom Property Manipulation

        public void ModifyProperties(PropertyDescriptorCollection col)
        {
            if (!TradesPerDirection)
            {
                col.Remove(col.Find(nameof(longPerDirection), true));
                col.Remove(col.Find(nameof(shortPerDirection), true));
            }
            if (!isEnableTime2)
            {
                col.Remove(col.Find(nameof(Start2), true));
                col.Remove(col.Find(nameof(End2), true));
            }
            if (!isEnableTime3)
            {
                col.Remove(col.Find(nameof(Start3), true));
                col.Remove(col.Find(nameof(End3), true));
            }
            if (!isEnableTime4)
            {
                col.Remove(col.Find(nameof(Start4), true));
                col.Remove(col.Find(nameof(End4), true));
            }
            if (!isEnableTime5)
            {
                col.Remove(col.Find(nameof(Start5), true));
                col.Remove(col.Find(nameof(End5), true));
            }
            if (!isEnableTime6)
            {
                col.Remove(col.Find(nameof(Start6), true));
                col.Remove(col.Find(nameof(End6), true));
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

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            PropertyDescriptorCollection orig = TypeDescriptor.GetProperties(GetType(), attributes);
            PropertyDescriptor[] arr = new PropertyDescriptor[orig.Count];
            orig.CopyTo(arr, 0);
            PropertyDescriptorCollection col = new PropertyDescriptorCollection(arr);

            ModifyProperties(col);

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

        #region Properties - Release Notes

        [ReadOnly(true)]
        [NinjaScriptProperty]
        [Display(Name = "Base Algo Version", Order = 1, GroupName = "01a. Release Notes")]
        public string BaseAlgoVersion { get; set; }

        [ReadOnly(true)]
        [NinjaScriptProperty]
        [Display(Name = "Author", Order = 2, GroupName = "01a. Release Notes")]
        public string Author { get; set; }

        [ReadOnly(true)]
        [NinjaScriptProperty]
        [Display(Name = "Strategy Name", Order = 3, GroupName = "01a. Release Notes")]
        public string StrategyName { get; set; }

        [ReadOnly(true)]
        [NinjaScriptProperty]
        [Display(Name = "Version", Order = 4, GroupName = "01a. Release Notes")]
        public string Version { get; set; }

        [ReadOnly(true)]
        [NinjaScriptProperty]
        [Display(Name = "Credits", Order = 5, GroupName = "01a. Release Notes")]
        public string Credits { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Chart Type", Order = 6, GroupName = "01a. Release Notes")]
        public string ChartType { get; set; }

        /*[NinjaScriptProperty]
        [Display(Name = "ATM Strategy Template", Order = 7, GroupName = "01a. Release Notes")]
        public string ATMStrategyTemplate { get; set; }*/

                // added sudo mod
        [TypeConverter(typeof(FriendlyAtmConverter))] // Converts the bool to string values
        [PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")] // Create the combo box on the property grid
        [Display(Name = "ATM Strategy Template", Order = 7, GroupName = "01a. Release Notes")]
        public string ATMStrategyTemplate { get; set; }

                // added sudo mod
        #region AtmStrategySelector converter
        // Since this is only being applied to a specific property rather than the whole class,
        // we don't need to inherit from IndicatorBaseConverter and can just use a generic TypeConverter
        public class FriendlyAtmConverter : TypeConverter
        {
            // Set the values to appear in the combo box
            public override StandardValuesCollection GetStandardValues(
                ITypeDescriptorContext context
            )
            {
                List<string> values = new List<string>();
                string[] files = System.IO.Directory.GetFiles(
                    System.IO.Path.Combine(
                        NinjaTrader.Core.Globals.UserDataDir,
                        "templates",
                        "AtmStrategy"
                    ),
                    "*.xml"
                );

                foreach (string atm in files)
                {
                    values.Add(System.IO.Path.GetFileNameWithoutExtension(atm));
                    NinjaTrader.Code.Output.Process(
                        System.IO.Path.GetFileNameWithoutExtension(atm),
                        PrintTo.OutputTab1
                    );
                }

                return new StandardValuesCollection(values);
            }

            public override object ConvertFrom(
                ITypeDescriptorContext context,
                System.Globalization.CultureInfo culture,
                object value
            )
            {
                return value.ToString();
            }

            public override object ConvertTo(
                ITypeDescriptorContext context,
                System.Globalization.CultureInfo culture,
                object value,
                Type destinationType
            )
            {
                return value;
            }

            // required interface members needed to compile
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return true;
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return true;
            }

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                return true;
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }
        }
        #endregion

        #endregion

        #region 01b. Support Developer

        [ReadOnly(true)]
        [NinjaScriptProperty]
        [Display(
            Name = "PayPal Donation URL",
            Order = 1,
            GroupName = "01b. Support Developer",
            Description = "https://www.paypal.com/signin"
        )]
        public string paypal { get; set; }

        #endregion

        #region Properties - Order Settings

        [NinjaScriptProperty]
        [Display(Name = "Order Type", Order = 1, GroupName = "02. Order Settings")]
        public OrderType orderType { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Limit Order Offset", Order = 2, GroupName = "02. Order Settings")]
        public int LimitOffset { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Breakeven Offset", Order = 3, GroupName = "02. Order Settings")]
        public int BreakevenOffset { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Tick Move", Order = 4, GroupName = "02. Order Settings")]
        public int TickMove { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Exit", Order = 5, GroupName = "02. Order Settings")]
        public bool enableExit { get; set; }

        #endregion

        #region Properties - Profit/Loss Limit

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Daily Loss / Profit ",
            Description = "Enable / Disable Daily Loss & Profit control",
            Order = 1,
            GroupName = "05. Profit/Loss Limit	"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool dailyLossProfit { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(
            ResourceType = typeof(Custom.Resource),
            Name = "Daily Profit Limit ($)",
            Description = "No positive or negative sign, just integer",
            Order = 2,
            GroupName = "05. Profit/Loss Limit	"
        )]
        public double DailyProfitLimit { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(
            ResourceType = typeof(Custom.Resource),
            Name = "Daily Loss Limit ($)",
            Description = "No positive or negative sign, just integer",
            Order = 3,
            GroupName = "05. Profit/Loss Limit	"
        )]
        public double DailyLossLimit { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Trailing Drawdown",
            Description = "Enable / Disable trailing drawdown",
            Order = 4,
            GroupName = "05. Profit/Loss Limit	"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool enableTrailingDD { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(
            ResourceType = typeof(Custom.Resource),
            Name = "Trailing Drawdown ($)",
            Description = "No positive or negative sign, just integer",
            Order = 5,
            GroupName = "05. Profit/Loss Limit	"
        )]
        public double TrailingDrawdown { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(
            ResourceType = typeof(Custom.Resource),
            Name = "Start Trailing Drawdown ($)",
            Description = "No positive or negative sign, just integer",
            Order = 6,
            GroupName = "05. Profit/Loss Limit	"
        )]
        public double StartTrailingDD { get; set; }

        #endregion

        #region Properties - Trades Per Direction

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Trades Per Direction",
            Description = "Switch off Historical Trades to use this option.",
            Order = 1,
            GroupName = "06. Trades Per Direction"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool TradesPerDirection
        {
            get { return tradesPerDirection; }
            set { tradesPerDirection = (value); }
        }

        [NinjaScriptProperty]
        [Display(
            Name = "Long Per Direction",
            Description = "Number of long in a row",
            Order = 2,
            GroupName = "06. Trades Per Direction"
        )]
        public int longPerDirection { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Short Per Direction",
            Description = "Number of short in a row",
            Order = 3,
            GroupName = "06. Trades Per Direction"
        )]
        public int shortPerDirection { get; set; }

        #endregion

        #region Properties - Default Settings

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
        [Display(
            Name = "Enable Inner Regression Channel",
            Order = 9,
            GroupName = "08b. Default Settings"
        )]
        public bool enableRegChan2 { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Show Outer Regression Channel",
            Order = 10,
            GroupName = "08b. Default Settings"
        )]
        public bool showRegChan1 { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Show Inner Regression Channel",
            Order = 11,
            GroupName = "08b. Default Settings"
        )]
        public bool showRegChan2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show High and Low Lines", Order = 12, GroupName = "08b. Default Settings")]
        public bool showRegChanHiLo { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Regression Channel Period",
            Order = 13,
            GroupName = "08b. Default Settings"
        )]
        public int RegChanPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Outer Regression Channel Width",
            Order = 14,
            GroupName = "08b. Default Settings"
        )]
        public double RegChanWidth { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Inner Regression Channel Width",
            Order = 15,
            GroupName = "08b. Default Settings"
        )]
        public double RegChanWidth2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Momo", Order = 16, GroupName = "08b. Default Settings")]
        public bool enableMomo { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Momentum", Order = 17, GroupName = "08b. Default Settings")]
        public bool showMomo { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Momo Up", Order = 18, GroupName = "08b. Default Settings")]
        public int MomoUp { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Momo Down", Order = 19, GroupName = "08b. Default Settings")]
        public int MomoDown { get; set; }

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
        [Display(Name = "ADX Threshold", Order = 23, GroupName = "08b. Default Settings")]
        public int adxThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Threshold 2", Order = 24, GroupName = "08b. Default Settings")]
        public int adxThreshold2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Volatility", Order = 25, GroupName = "08b. Default Settings")]
        public bool enableVolatility { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ATR Period", Order = 26, GroupName = "08b. Default Settings")]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volatility Threshold", Order = 27, GroupName = "08b. Default Settings")]
        public double atrThreshold { get; set; }

        #endregion

        #region Properties - Market Condition

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Chop Detection",
            Order = 1,
            GroupName = "09. Market Condition"
        )]
        public bool EnableChoppinessDetection { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(
            Name = "Regression Channel Look Back Period",
            Description = "Period for Regression Channel used in chop detection.",
            Order = 2,
            GroupName = "09. Market Condition"
        )]
        public int SlopeLookBack { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 1.0)] // Factor less than 1 to indicate narrower than average
        [Display(
            Name = "Flat Slope Factor",
            Description = "Factor of slope of Regression Channel indicates flatness.",
            Order = 3,
            GroupName = "09. Market Condition"
        )]
        public double FlatSlopeFactor { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(
            Name = "Chop ADX Threshold",
            Description = "ADX value below which the market is considered choppy (if RegChan is also flat).",
            Order = 4,
            GroupName = "09. Market Condition"
        )]
        public int ChopAdxThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Background Color Signal",
            Description = "Enable Exit",
            Order = 5,
            GroupName = "09. Market Condition"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool enableBackgroundSignal { get; set; }

        #endregion

        #region Properties - Timeframes

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Trades", Order = 1, GroupName = "10. Timeframes")]
        public DateTime Start { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Trades", Order = 2, GroupName = "10. Timeframes")]
        public DateTime End { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Time 2",
            Description = "Enable 2 times.",
            Order = 3,
            GroupName = "10. Timeframes"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool Time2
        {
            get { return isEnableTime2; }
            set { isEnableTime2 = (value); }
        }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time 2", Order = 4, GroupName = "10. Timeframes")]
        public DateTime Start2 { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time 2", Order = 5, GroupName = "10. Timeframes")]
        public DateTime End2 { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Time 3",
            Description = "Enable 3 times.",
            Order = 6,
            GroupName = "10. Timeframes"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool Time3
        {
            get { return isEnableTime3; }
            set { isEnableTime3 = (value); }
        }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time 3", Order = 7, GroupName = "10. Timeframes")]
        public DateTime Start3 { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time 3", Order = 8, GroupName = "10. Timeframes")]
        public DateTime End3 { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Time 4",
            Description = "Enable 4 times.",
            Order = 9,
            GroupName = "10. Timeframes"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool Time4
        {
            get { return isEnableTime4; }
            set { isEnableTime4 = (value); }
        }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time 4", Order = 10, GroupName = "10. Timeframes")]
        public DateTime Start4 { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time 4", Order = 11, GroupName = "10. Timeframes")]
        public DateTime End4 { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Time 5",
            Description = "Enable 5 times.",
            Order = 12,
            GroupName = "10. Timeframes"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool Time5
        {
            get { return isEnableTime5; }
            set { isEnableTime5 = (value); }
        }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time 5", Order = 13, GroupName = "10. Timeframes")]
        public DateTime Start5 { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time 5", Order = 14, GroupName = "10. Timeframes")]
        public DateTime End5 { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Enable Time 6",
            Description = "Enable 6 times.",
            Order = 15,
            GroupName = "10. Timeframes"
        )]
        [RefreshProperties(RefreshProperties.All)]
        public bool Time6
        {
            get { return isEnableTime6; }
            set { isEnableTime6 = (value); }
        }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time 6", Order = 16, GroupName = "10. Timeframes")]
        public DateTime Start6 { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time 6", Order = 17, GroupName = "10. Timeframes")]
        public DateTime End6 { get; set; }

        #endregion

        #region Properties - Status Panel

        [NinjaScriptProperty]
        [Display(Name = "Show Daily PnL", Order = 1, GroupName = "11. Status Panel")]
        public bool showDailyPnl { get; set; }

        [XmlIgnore()]
        [Display(Name = "Daily PnL Color", Order = 2, GroupName = "11. Status Panel")]
        public Brush colorDailyProfitLoss { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "Daily PnL Position",
            Description = "Daily PNL Alert Position",
            Order = 3,
            GroupName = "11. Status Panel"
        )]
        public TextPosition PositionDailyPNL { get; set; }

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
        public Brush colorPnl { get; set; }

        [NinjaScriptProperty]
        [Display(
            Name = "STATUS PANEL Position",
            Description = "Status PNL Position",
            Order = 6,
            GroupName = "11. Status Panel"
        )]
        public TextPosition PositionPnl { get; set; }

        // Serialize our Color object
        [Browsable(false)]
        public string colorPnlSerialize
        {
            get { return Serialize.BrushToString(colorPnl); }
            set { colorPnl = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(
            Name = "Show Historical Trades",
            Description = "Show Historical Teorical Trades",
            Order = 7,
            GroupName = "11. Status Panel"
        )]
        public bool ShowHistorical { get; set; }

        #endregion

        #region 12. WebHook

        //		[NinjaScriptProperty]
        //		[Display(Name="Activate Discord webhooks", Description="Activate One or more Discord webhooks", GroupName="11. Webhook", Order = 0)]
        //		public bool useWebHook { get; set; }

        //		[NinjaScriptProperty]
        //		[Display(Name="Discord webhooks", Description="One or more Discord webhooks, separated by comma.", GroupName="11. Webhook", Order = 2)]
        //		public string DiscordWebhooks
        //		{ get; set; }

        #endregion
    }
}
