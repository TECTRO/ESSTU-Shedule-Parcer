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
     //v                  mainScheduleParser.Grouping.DefaultLoadSchedules(ScheduleParser.GroupingMethods.DefaultLoadingStructs);

            var loaded =  mainScheduleParser.Grouping.DefaultLoadSchedules(ScheduleParser.GroupingMethods.DefaultLoadingStructs);
     

            #region asNewFutureMethod

            List<Schedule> schedules = new List<Schedule>();

            schedules.AddRange(
                mainScheduleParser.Sync.LoadSchedules(new[]
            {
                new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/raspisan.htm", NodeType.Student),
                new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/raspisan.htm", NodeType.Student)
            }));
            
            schedules.AddRange(mainScheduleParser.Sync.ConvertSchedulesToAuditoriums(schedules));
            
            schedules.AddRange(mainScheduleParser.Sync.LoadSchedules(new[]
            {
                new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/craspisanEdt.htm", NodeType.Teacher),
                new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/craspisanEdt.htm", NodeType.Teacher)
            }));

            GroupOfSchedule groupedSchedule = new GroupOfSchedule(schedules);
            
            mainScheduleParser.Grouping.CreateSubgroupsTree(groupedSchedule, GroupOfSchedule.DefaultFilters);
            
            mainScheduleParser.Grouping.RemoveGarbageFromTree(groupedSchedule);

            #endregion

            //todo укампановать набор комманд в новый метод дефолтлоад со входным параметром в виде структуры дефолтсурсес. Обьеденить загрузки через асинхронные методы внутри общего метода wait и после скомпоновать результат и выдать готовое групедшедул
            //todo добавить новый подкласс firebase и воткнуть туда методы аплоад довнлоад и синк
  
            ThreadManager.UseDebugger = false;

        }
    }
}
