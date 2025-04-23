#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.NinjaScript;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Strategies;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class WillyATM : ATMAlgoBase
    {
        private NinjaTrader.NinjaScript.Indicators.TradeSaber_SignalMod.TOWilliamsTraderOracleSignalMOD WilliamsR1;

		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "This strategy is based on William R indicator.";
                Name = "Willy ATM v5.2";
                StrategyName = "Willy ATM";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by dbt7 and Khanh Nguyen";
                ChartType =  "Orenko 34-40-40";	
				
				wrUp 			= -20;
				wrDown			= -80;
				wrPeriod		= 14;
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
			
			longSignal = WilliamsR1[1] >= wrUp && Close[0] > Close[1] && High[1] > High[2];
            shortSignal = WilliamsR1[1] <= wrDown && Close[0] < Close[1] && Low[1] < Low[2];	
			
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
            return enableExit? true : false;
        }

        protected override bool ValidateExitShort()
        {
			// Logic for validating short exits
			return enableExit? true : false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
			WilliamsR1    = TOWilliamsTraderOracleSignalMOD(Close, 14, @"LongEntry", @"ShortEntry");
			WilliamsR1.Plots[0].Brush = Brushes.Yellow;
			WilliamsR1.Plots[0].Width = 1;
			AddChartIndicator(WilliamsR1);
        }
        #endregion

        #region Properties

		[NinjaScriptProperty]
		[Display(Name="William R Period", Order=1, GroupName="08a. Strategy Settings")]
		public int wrPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Long Entry", Order=2, GroupName="08a. Strategy Settings")]
		public int wrUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Short Entry", Order=3, GroupName="08a. Strategy Settings")]
		public int wrDown
		{ get; set; }		
		
        #endregion
    }
}
