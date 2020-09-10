using ParseHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Metadata.Edm;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SheduleParcer
{
    class Program
    {
        //=================================================
        static void Main(string[] args)
        {

            ThreadManager.UseDebugger = true;
          
            ScheduleParser mainScheduleParser = new ScheduleParser(new ThreadManager());

            var loaded =  mainScheduleParser.Grouping.DefaultLoadSchedules(ScheduleParser.GroupingMethods.DefaultLoadingStructs);
     

            //todo добавить новый подкласс firebase и воткнуть туда методы аплоад довнлоад и синк
  
            ThreadManager.UseDebugger = false;

        }
    }
}
