#region Using declarations
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
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
    public class SuperBot2ATM : ATMAlgoBase
    {
		private BlueZ.BlueZHMAHooks hullMAHooks;
		private bool hmaHooksUp;
		private bool hmaHooksDown;
		private bool hmaUp;
		private bool hmaDown;
		
       	private RegressionChannel RegressionChannel1, RegressionChannel2;
		private RegressionChannelHighLow RegressionChannelHighLow1;
		private bool regChanUp;
		private bool regChanDown;
		
		private NinjaTrader.NinjaScript.Indicators.TradeSaber_SignalMod.TOWilliamsTraderOracleSignalMOD WilliamsR1;
		private bool WillyUp;
		private bool WillyDown;
		
		private Momentum Momentum1;
		private bool momoUp;
		private bool momoDown;
		
		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "SuperBot is a combination of many strategies and indicators, including HMA Hooks, Linear Regression Channel, Momentum, and William R.";
                Name = "SuperBot2 ATM v5.2";
                StrategyName = "SuperBot2 ATM";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType =  "Orenko 34-40-40";
					
				HmaPeriod			= 16;
				enableHmaHooks		= true;
				showHmaHooks		= true;
				
				RegChanPeriod		= 40;
				RegChanWidth		= 4;
				RegChanWidth2		= 3;
				enableRegChan1		= true;
				enableRegChan2		= true;
				showRegChan1		= true;
				showRegChan2		= true;
				showRegChanHiLo		= true;
				
				wrUp 				= -20;
				wrDown				= -80;
				wrPeriod			= 14;
				enableWilly			= true;
				showWilly			= false;
				
				MomoUp				= 1;
				MomoDown			= -1;
				enableMomo			= true;
				showMomo			= true;				
            }
            else if (State == State.DataLoaded)
            {
                InitializeIndicators();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < BarsRequiredToTrade)
                return;
			
			bool channelSlopeUp = (RegressionChannel1.Middle[1] > RegressionChannel1.Middle[2]) && (RegressionChannel1.Middle[2] <= RegressionChannel1.Middle[3]) 
				|| (RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1] && Low[0] > Low[2] && Low[2] <= RegressionChannel1.Lower[2]);
    		bool priceNearLowerChannel = (Low[0] > RegressionChannelHighLow1.Lower[2]);

			bool channelSlopeDown = (RegressionChannel1.Middle[1] < RegressionChannel1.Middle[2]) && (RegressionChannel1.Middle[2] >= RegressionChannel1.Middle[3])
				|| (RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1] && High[0] < High[2] && High[2] >= RegressionChannel1.Upper[2]);
    		bool priceNearUpperChannel = (High[0] < RegressionChannelHighLow1.Upper[2]);

            regChanUp = enableRegChan1 ? channelSlopeUp || priceNearLowerChannel : true;
            regChanDown = enableRegChan1 ? channelSlopeDown || priceNearUpperChannel : true;
			
			hmaHooksUp = enableHmaHooks ? (Close[0] > hullMAHooks[0] && hullMAHooks.trend[0] == 1 && hullMAHooks.trend[1] == -1) 
				|| (hullMAHooks[0] > hullMAHooks[2]) : true;
			
			hmaHooksDown = enableHmaHooks ? (Close[0] < hullMAHooks[0] && hullMAHooks.trend[0] == -1 && hullMAHooks.trend[1] == 1)  
				|| (hullMAHooks[0] < hullMAHooks[2]) : true;
			
			hmaUp = Close[0] > hullMAHooks[0];
			hmaDown = Close[0] < hullMAHooks[0];
			
			WillyUp = enableWilly ? WilliamsR1[1] >= wrUp && Close[0] > Close[1] && High[1] > High[2] : true;
            WillyDown = enableWilly ? WilliamsR1[1] <= wrDown && Close[0] < Close[1] && Low[1] < Low[2] : true;
			
			momoUp = enableMomo ? Momentum1[0] > MomoUp && Momentum1[0] > Momentum1[1] : true;;
			momoDown = enableMomo ? Momentum1[0] < MomoDown && Momentum1[0] < Momentum1[1] : true;
			
			longSignal = hmaHooksUp || regChanUp || WillyUp || momoUp;
            shortSignal = hmaHooksDown || regChanDown || WillyDown || momoDown; 
			
			base.OnBarUpdate();
        }

        protected override bool ValidateEntryLong()
        {
            // Logic for validating long entries
			if (longSignal) return true;
			else return false;
        }

        protected override bool ValidateEntryShort()
        {
            // Logic for validating short entries
			if (shortSignal) return true;
            else return false;
        }

       	protected override bool ValidateExitLong()
        {
            // Logic for validating long exits
            return false;
        }

        protected override bool ValidateExitShort()
        {
			// Logic for validating short exits
			return false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
			hullMAHooks	= BlueZHMAHooks(Close, HmaPeriod, 0, false, false, true, Brushes.Lime, Brushes.Red);
			hullMAHooks.Plots[0].Brush = Brushes.White;
			hullMAHooks.Plots[0].Width = 2;
			if (showHmaHooks) AddChartIndicator(hullMAHooks);
				
            RegressionChannel1			= RegressionChannel(Close, RegChanPeriod, RegChanWidth);
			if (showRegChan1) AddChartIndicator(RegressionChannel1);
			
            RegressionChannel2			= RegressionChannel(Close, RegChanPeriod, RegChanWidth2);
			if (showRegChan2) AddChartIndicator(RegressionChannel2);
			
			RegressionChannelHighLow1	= RegressionChannelHighLow(Close, RegChanPeriod, RegChanWidth);	
			if (showRegChanHiLo) AddChartIndicator(RegressionChannelHighLow1);
					
			WilliamsR1    = TOWilliamsTraderOracleSignalMOD(Close, 14, @"LongEntry", @"ShortEntry");
			WilliamsR1.Plots[0].Brush = Brushes.Yellow;
			WilliamsR1.Plots[0].Width = 1;
			if (showWilly) AddChartIndicator(WilliamsR1);
			
			Momentum1			= Momentum(Close, 14);	
			Momentum1.Plots[0].Brush = Brushes.Yellow;
			Momentum1.Plots[0].Width = 2;
			if (showMomo) AddChartIndicator(Momentum1);				
        }
        #endregion


		#region Properties - Strategy Settings
	
		[NinjaScriptProperty]
        [Display(Name = "Enable Hooker", Order = 1, GroupName = "08a. Strategy Settings")]
        public bool enableHmaHooks { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="HMA Period", Order = 2, GroupName="08a. Strategy Settings")]
		public int HmaPeriod
		{ get; set; }

		[NinjaScriptProperty]
        [Display(Name = "Show HMA Hooks", Order = 3, GroupName = "08a. Strategy Settings")]
        public bool showHmaHooks { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable KingKhanh", Order = 4, GroupName = "08a. Strategy Settings")]
        public bool enableRegChan1 { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Enable Inner Regression Channel", Order = 5, GroupName = "08a. Strategy Settings")]
        public bool enableRegChan2 { get; set; }
        
		[NinjaScriptProperty]
		[Display(Name="Regression Channel Period", Order = 6, GroupName="08a. Strategy Settings")]
		public int RegChanPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Outer Regression Channel Width", Order = 7, GroupName="08a. Strategy Settings")]
		public double RegChanWidth
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Inner Regression Channel Width", Order = 8, GroupName="08a. Strategy Settings")]
		public double RegChanWidth2
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Outer Regression Channel", Order = 9, GroupName = "08a. Strategy Settings")]
        public bool showRegChan1 { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Show Inner Regression Channel", Order = 10, GroupName = "08a. Strategy Settings")]
        public bool showRegChan2 { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Show High and Low Lines", Order = 11, GroupName = "08a. Strategy Settings")]
        public bool showRegChanHiLo { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Enable Willy", Order = 12, GroupName = "08a. Strategy Settings")]
        public bool enableWilly { get; set; }
        
		[NinjaScriptProperty]
		[Display(Name="Willy Period", Order = 13, GroupName="08a. Strategy Settings")]
		public int wrPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Willy Up", Order = 14, GroupName="08a. Strategy Settings")]
		public int wrUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Willy Down", Order = 15, GroupName="08a. Strategy Settings")]
		public int wrDown
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Willy", Order = 16, GroupName = "08a. Strategy Settings")]
        public bool showWilly { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable Momo", Order = 17, GroupName = "08a. Strategy Settings")]
        public bool enableMomo { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Momo Up", Order = 18, GroupName="08a. Strategy Settings")]
		public int MomoUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Momo Down", Order = 19, GroupName="08a. Strategy Settings")]
		public int MomoDown
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Momentum", Order = 20, GroupName = "08a. Strategy Settings")]
        public bool showMomo { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable SuperRex", Order = 21, GroupName = "08a. Strategy Settings")]
        public bool enableSuperRex { get; set; }
        
		[NinjaScriptProperty]
		[Display(Name="CMO Up", Order = 22, GroupName="08a. Strategy Settings")]
		public int CmoUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="CMO Down", Order = 23, GroupName="08a. Strategy Settings")]
		public int CmoDown
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show CMO", Order = 24, GroupName = "08a. Strategy Settings")]
        public bool showCMO { get; set; }
		
		#endregion
	}
}
