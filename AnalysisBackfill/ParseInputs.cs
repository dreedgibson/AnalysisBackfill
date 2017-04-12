using OSIsoft.AF;
using OSIsoft.AF.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;

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

namespace AnalysisUtil
{
    public class ParseInputs
    {
        public static object[] All(string[] args)
        {
            //define modes
            List<string> modes = new List<string>()
            {
                "printanalyses",
                "startanalyses",
                "stopanalyses"
            };

            //define help
            String help_message = "This utility backfills/recalculates analyses.  Generic syntax: "
                + "\n\tAnalysisBackfill.exe \\\\AFServer\\AFDatabase\\pathToElement\\AFElement AnalysisNameFilter StartTime EndTime Mode"
                + "\n This utility supports two modes: backfill and recalc.  Backfill will fill in data gaps only.  Recalc will replace all values.  Examples:"
                + "\n\tAnalysisBackfill.exe \\\\AF1\\TestDB\\Plant1\\Pump1 FlowRate_*Avg '*-10d' '*' recalc"
                + "\n\tAnalysisBackfill.exe \\\\AF1\\TestDB\\Plant1\\Pump1 *Rollup '*-10d' '*' backfill";

            List<string> helps = new List<string>()
            {
                help_message
            };

            //define variables
            string scope = null;
            AFDatabase aDatabase = null;
            string elementPath = null;

            //determine mode
            string user_mode = args[0].ToLower();
            if (args.Length < 2 || args.Contains("?") || !modes.Contains(user_mode))
            {
                Console.WriteLine("This analysis utility has the following modes:");
                foreach (var help in helps) Console.WriteLine(help);
                Environment.Exit(0);
            }

            //parse inputs
            string user_path = args[1];
            var inputs = user_path.Split('\\');
            var inputsLength = inputs.Length;

            //server
            string user_serv = inputs[2];
            PISystem aSystem = PIConnection.ConnectAF(user_serv);
            if (inputsLength == 3)
                scope = "server";
            else
            {
                //database
                string user_db = inputs[3];
                aDatabase = (AFDatabase)PIConnection.ConnectAF(user_serv, user_db).ElementAt(1);
                if (inputsLength == 4)
                    scope = "database";

                //element
                else if (inputsLength > 4)
                {
                    scope = "element";
                    var preLength = user_serv.Length + user_db.Length;
                    elementPath = user_path.Substring(preLength + 3, user_path.Length - preLength - 3);
                }
            }

            //return object array
            object[] parsed =
            {
                aSystem,
                aDatabase,
                elementPath,
                scope,
                user_mode
            };
            return parsed;
        }

        public static AFAnalysisService.CalculationMode DetermineMode(string user_mode)
        {
            AFAnalysisService.CalculationMode mode = AFAnalysisService.CalculationMode.FillDataGaps;
            switch (user_mode.ToLower())
            {
                case "recalc":
                    mode = AFAnalysisService.CalculationMode.DeleteExistingData;
                    break;
                case "backfill":
                    break;
                default:
                    Console.WriteLine("Invalid mode specified.  Supported modes: backfill, recalc");
                    Environment.Exit(0);
                    break;
            }
            return mode;
        }
    }

}
