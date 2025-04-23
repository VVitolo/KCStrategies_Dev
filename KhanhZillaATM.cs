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
using RegressionChannel = NinjaTrader.NinjaScript.Indicators.RegressionChannel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class KhanhZillaATM : ATMAlgoBase2
    {
		private RegressionChannelHighLow RegressionChannelHighLow1;		
		
		private double highestHigh;
		private double lowestLow;
		
		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "Strategy based on the Linear Regression Channel indicator.";
                Name = "KhanhZilla ATM v5.2";
                StrategyName = "KhanhZilla ATM";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType = "30 Second Chart";		

				RegChanPeriod	= 20;
				RegChanWidth	= 4;
				
				TickDistance	= 24;
				
				enableExit = true;
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
			
			longSignal = (Low[0] > RegressionChannelHighLow1.Lower[1]) 
				&& (Low[1] == RegressionChannelHighLow1.Lower[1]);

            shortSignal =  (High[0] < RegressionChannelHighLow1.Upper[1])
				&& (High[1] == RegressionChannelHighLow1.Upper[1]);
			
			if (longSignal)				
				lowestLow = RegressionChannelHighLow1.Lower[1];
			
			if (shortSignal)
				highestHigh = RegressionChannelHighLow1.Upper[1];

			exitLong = Low[0] < lowestLow - TickDistance * TickSize;
			exitShort = High[0] > highestHigh + TickDistance * TickSize;
			
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
            if (exitLong) return true;
            else return false;
        }

        protected override bool ValidateExitShort()
        {
			if (exitShort) return true;
			else return false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
			RegressionChannelHighLow1 = RegressionChannelHighLow(Close, Convert.ToInt32(RegChanPeriod), RegChanWidth);		
			RegressionChannelHighLow1.Plots[0].Width = 2;
			RegressionChannelHighLow1.Plots[1].Width = 2;
            RegressionChannelHighLow1.Plots[1].Brush = Brushes.Yellow;
			RegressionChannelHighLow1.Plots[2].Width = 2;
            RegressionChannelHighLow1.Plots[2].Brush = Brushes.Yellow;
			AddChartIndicator(RegressionChannelHighLow1);
        }
        #endregion


		#region Properties - Strategy Settings
	
		[NinjaScriptProperty]
		[Display(Name="Tick Distance from High / Low Lines", Order=2, GroupName="02. Order Settings")]
		public int TickDistance
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Regression Channel Period", Order=1, GroupName="08a. Strategy Settings")]
		public int RegChanPeriod
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Regression Channel Width", Order=2, GroupName="08a. Strategy Settings")]
		public double RegChanWidth
		{ get; set; }

		#endregion
	}
}
