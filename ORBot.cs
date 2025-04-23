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
    public class ORBot : KCAlgoBase
    {
        // Parameters
		private NinjaTrader.NinjaScript.Indicators.TradeSaber.ORB_TradeSaber ORB_TradeSaber1;

		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "Strategy based on Open Range Breakout.";
                Name = "ORBot v5.2";
                StrategyName = "ORBot";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType =  "1 Minute Chart";	
				
				showORB			= true;
				
				OrbStartTime	= "06:30 AM";
				OrbEndTime		= "06:50 AM";
				
                InitialStop		= 310;
				ProfitTarget	= 400;
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
			
//            longSignal = CrossAbove(Close, ORB_TradeSaber1.Signal[0], 1);
//            shortSignal = CrossBelow(Close, ORB_TradeSaber1.Signal[0], 1);
			
			longSignal = (ORB_TradeSaber1.Signal[0] == 1);
            shortSignal = (ORB_TradeSaber1.Signal[0] == -1); 
			
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
			ORB_TradeSaber1	= ORB_TradeSaber(Close, true, 0.2, DateTime.Parse(OrbStartTime), DateTime.Parse(OrbEndTime), @"PST", @"Recommended that PC and 
NinjaTrader clocks match", true, @"AboveUpper", @"BelowLower", true, @"AboveLower", @"BelowUpper", false, false, @"AboveLower", @"BelowUpper", false, @"TradeSaber - Built With Grok", @"Version 1.0 // March 2025", @"https://tradesaber.com/predator-guide/", @"https://Discord.gg/2YU9GDme8j", @"https://youtu.be/jUYT-Erzc_8");
			ORB_TradeSaber1.Plots[0].Brush = Brushes.Aqua;
			ORB_TradeSaber1.Plots[1].Brush = Brushes.Yellow;
			ORB_TradeSaber1.Plots[2].Brush = Brushes.Transparent;
			ORB_TradeSaber1.Plots[0].Width = 2;
			ORB_TradeSaber1.Plots[1].Width = 2;
			if (showORB) AddChartIndicator(ORB_TradeSaber1);
        }
        #endregion

        #region Properties

		[NinjaScriptProperty]
		[Display(Name = "Show Open Range Breakout", Order = 1, GroupName = "08a. Strategy Settings")]
		public bool showORB { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name="ORB Start Time", Order=2, GroupName="03. Strategy Settings")]
		public String OrbStartTime
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="ORB End Time", Order=3, GroupName="03. Strategy Settings")]
		public String OrbEndTime
		{ get; set; }

        #endregion
    }
}
