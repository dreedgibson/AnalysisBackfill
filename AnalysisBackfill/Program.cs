using OSIsoft.AF.Analysis;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Time;
using OSIsoft.AF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF.PI;
using OSIsoft.AF.Search;
using System.Threading;
using AnalysisUtil;

/*
 *  Copyright (C) 2017  Keith Fong

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace AnalysisBackfill
{
    class AnalysisBackfill
    {
        static void Main(string[] args)
        {
            //define variables
            string elementTemplateName = null;
            string fullPath = null;
            string elementName = null;
            string analysisName = "*";
            string user_startTime = null;
            string user_endTime = null;
            string user_mode = null;

            AFElement rootElement = new AFElement();
            List<AFElement> foundElements = new List<AFElement>();
            List<AFAnalysis> foundAnalyses = new List<AFAnalysis>();
            IEnumerable<AFAnalysis> elemAnalyses = null;

            AFTimeRange backfillPeriod = new AFTimeRange();

            AFAnalysisService.CalculationMode mode = AFAnalysisService.CalculationMode.FillDataGaps;
            String reason = null;
            Object response = null;

            String help_message = "This utility backfills/recalculates analyses.  Generic syntax: "
                            + "\n\tAnalysisBackfill.exe \\\\AFServer\\AFDatabase\\pathToElement\\AFElement AnalysisNameFilter StartTime EndTime Mode"
                            + "\n This utility supports two modes: backfill and recalc.  Backfill will fill in data gaps only.  Recalc will replace all values.  Examples:"
                            + "\n\tAnalysisBackfill.exe \\\\AF1\\TestDB\\Plant1\\Pump1 FlowRate_*Avg '*-10d' '*' recalc"
                            + "\n\tAnalysisBackfill.exe \\\\AF1\\TestDB\\Plant1\\Pump1 *Rollup '*-10d' '*' backfill"
                            + "\n\n /elementtemplate, /rootelement, /elementname, /analysisname, /starttime, /endtime, /mode";

            try
            {
                //parse inputs and connect
                foreach (var arg_n in args)
                {
                    var arg = arg_n.ToLower();
                    if (arg.Contains("/elementtemplate")) elementTemplateName = arg.Split(':')[1];
                    if (arg.Contains("/fullpath")) fullPath = arg.Split(':')[1];
                    if (arg.Contains("/elementname")) elementName = arg.Split(':')[1];
                    if (arg.Contains("/analysisName")) analysisName = arg.Split(':')[1];
                    if (arg.Contains("/starttime")) user_startTime = arg.Split(':')[1];
                    if (arg.Contains("/endtime")) user_endTime = arg.Split(':')[1];
                    if (arg.Contains("/mode")) user_mode = arg.Split(':')[1];
                }

                var pathArray = fullPath.Split('\\');
                var user_serv = pathArray[2];
                var user_db = pathArray[3];
                var connectionVars = PIConnection.ConnectAF(user_serv, user_db);
                PISystem aSystem = (PISystem)connectionVars[0];
                AFDatabase aDatabase = (AFDatabase)connectionVars[1];
                AFAnalysisService aAnalysisService = aSystem.AnalysisService;

                var prelength = user_serv.Length + user_db.Length;
                var user_rootElement = fullPath.Substring(prelength + 4, fullPath.Length - prelength - 4);
                if (user_rootElement != "")
                    rootElement = (AFElement)AFObject.FindObject(user_rootElement, aDatabase);

                //bad input handling & help
                if (args.Contains("?") || user_startTime == null || user_endTime == null || user_mode == null)
                {
                    Console.WriteLine(help_message);
                    Environment.Exit(0);
                }

                //check versions
                /*
                aSystem.ServerVersion;
                aSystems.Version
                aPIServer.ServerVersion
                */

                //time range
                AFTime startTime = new AFTime(user_startTime.Trim('\''));
                AFTime endTime = new AFTime(user_endTime.Trim('\''));
                backfillPeriod = new AFTimeRange(startTime, endTime);

                mode = ParseInputs.DetermineMode(user_mode);

                #region findelements
                //find AFElements

                //AFElement.FindElements(aDatabase, )

                if (user_rootElement == "") //all elements in database
                    foundElements = AFElement.FindElements(aDatabase, null, null, AFSearchField.Name, true, AFSortField.Name, AFSortOrder.Ascending, 1000).ToList();
                else if (user_rootElement != "" && elementName != "") 
                    {
                        AFElement rootElement = (AFElement)AFObject.FindObject(user_rootElement, aDatabase);
                        foundElements = AFElement.FindElements(aDatabase, rootElement, elementName, AFSearchField.Name, true, AFSortField.Name, AFSortOrder.Ascending, 1000).ToList();
                    }
                else //single element
                    foundElements.Add((AFElement)AFObject.FindObject(user_rootElement, aDatabase));
                #endregion

                Console.WriteLine("Requested backfills/recalculations:");
                foreach (AFElement elem_n in foundElements)
                {
                    #region FindAnalyses
                    String analysisfilter = "Target:=\"" + elem_n.GetPath(aDatabase) + "\" Name:=\"" + analysisName + "\"";
                    AFAnalysisSearch analysisSearch = new AFAnalysisSearch(aDatabase, "analysisSearch", AFAnalysisSearch.ParseQuery(analysisfilter));
                    elemAnalyses = analysisSearch.FindAnalyses(0, true).ToList();

                    //print details to user
                    Console.WriteLine("\tElement: " + elem_n.GetPath().ToString()
                        + "\n\tAnalyses (" + elemAnalyses.Count() + "):");
                    
                    if (elemAnalyses.Count() == 0)
                        Console.WriteLine("\t\tNo analyses on this AF Element match the analysis filter.");
                    else
                    {
                        foundAnalyses.AddRange(elemAnalyses);
                        foreach (var analysis_n in elemAnalyses)
                        {
                            Console.WriteLine("\t\t{0}, {1}, Outputs:{2}", analysis_n.Name, analysis_n.AnalysisRule.Name, analysis_n.AnalysisRule.GetOutputs().Count);
                        }
                    }

                    /* to check for dependent analyses
                    foreach (var analysis_n in foundAnalyses)
                    {

                    }
                    */
                    #endregion
                }

                #region QueueAnalyses
                Console.WriteLine("\nTime range: " + backfillPeriod.ToString() + ", " + "{0}d {1}h {2}m {3}s."
                            , backfillPeriod.Span.Days, backfillPeriod.Span.Hours, backfillPeriod.Span.Minutes, backfillPeriod.Span.Seconds);
                Console.WriteLine("Mode: " + user_mode + "=" + mode.ToString());
                
                //implement wait time
                Console.WriteLine("\nA total of {0} analyses will be queued for processing in 10 seconds.  Press Ctrl+C to cancel.", foundAnalyses.Count);
                DateTime beginWait = DateTime.Now;
                while (!Console.KeyAvailable && DateTime.Now.Subtract(beginWait).TotalSeconds < 10)
                {
                    Console.Write(".");
                    Thread.Sleep(250);
                }

                //no status check
                Console.WriteLine("\n\nAll analyses have been queued.\nThere is no status check after the backfill/recalculate is queued (until AF 2.9.0). Please verify by using other means.", foundAnalyses.Count);

                //queue analyses for backfill/recalc
                foreach (var analysis_n in foundAnalyses)
                {
                    response = aAnalysisService.QueueCalculation(new List<AFAnalysis> { analysis_n }, backfillPeriod, mode);

                    /* no status check info
                        * in AF 2.9, QueueCalculation will allow for true status checking. In AF 2.8.5, it is not possible to check.  
                        * Documentation (https://techsupport.osisoft.com/Documentation/PI-AF-SDK/html/M_OSIsoft_AF_Analysis_AFAnalysisService_ToString.htm) states:
                        *This method queues the list of analyses on the analysis service to be calculated. 
                        * The operation is asynchronous and returning of the method does not indicate that queued analyses were calculated. 
                        * The status can be queried in the upcoming releases using the returned handle.
                    */

                    //Might be able to add a few check mechanisms using AFAnalysis.GetResolvedOutputs and the number of values in AFTimeRange
                }
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error returned: " + ex.Message);
                Environment.Exit(0);
            }
        }
    }
}