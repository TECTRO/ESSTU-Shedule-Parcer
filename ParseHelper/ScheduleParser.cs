using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace ParseHelper
{
    public partial class ScheduleParser
    {
        private ThreadManager ThManager { get; }
        public ScheduleParser(ThreadManager manager)
        {
            ThManager = manager;
            Sync = new SyncMethods(this);
            Async = new AsyncMethods(this);
            Grouping = new GroupingMethods(this);
            Common = new CommonMethods(this);
            FireBase = new FireBaseMethods(this);
        }

        public delegate void GerLinksDelegate(string tableLink, string tableData);

        ///REGEX-ES//////////////////////////////////////////////////////////
        protected static Regex WebLinkMask = new Regex(@"(?<mask>\S+)/\S+\.\S+", RegexOptions.IgnoreCase);
        protected static Regex WebLinkAddress = new Regex(@"href=""(?<address>\S+\.\S+)"".*<FONT.+>(?<linkname>.+)</FONT>", RegexOptions.IgnoreCase);
        protected static Regex WebLinkAddressShort = new Regex(@"href=""(?<address>\S+\.\S+)""", RegexOptions.IgnoreCase);

        public static readonly Regex Auditory = new Regex(@"а\.(?<audit>\d*-?\w*-?\w*)"); //@"\d?\d?-?\d{2}?\d?-?\d?"
        public static readonly Regex ProfessorFio = new Regex(@"\w+\s\w[\.\,]\s?\w[\.\,]?");
        public static readonly Regex Group = new Regex(@"(?<group>\w*\s?\d\d\d?\d?/?\d*,?-?\d*)");
        /////////////////////////////////////////////////////////////////////

        public delegate void ExceptionDelegate(Exception exc);
        public event ExceptionDelegate ExceptionEvent;

        ///synchronization plug for async recursive methods
        private readonly object _synchronizationPlug = new object();
        ///

        private IEnumerable<string> MySplit(string value, string splitter)
        {
            List<string> result = new List<string>();

            var splitVal = value.IndexOf(splitter, StringComparison.Ordinal);

            result.Add(value.Substring(0, splitVal));
            result.Add(value.Remove(0, splitVal + splitter.Length));
            return result;
        }

        private static TextInfo _info;
        private string CorrectRegister(string source)
        {
            if (_info == null) _info = new CultureInfo("en-US", false).TextInfo;

            return _info.ToTitleCase(source.ToLower());
        }
        
        /// <summary>
        /// основная функция, на которой все построено
        /// </summary>
        /// <param name="type"></param>
        /// <param name="mainData"></param>
        /// <returns></returns>
        private IEnumerable<Schedule> FillTable(NodeType type, string mainData)
        {
            //    Regex Auditory = new Regex(@"а\.(?<audit>\d*-?\d*\w*)"); //@"\d?\d?-?\d{2}?\d?-?\d?"
            //    Regex ProfessorFio = new Regex(@"\w+\s\w\.\s?\w\.");
            //    Regex Group = new Regex(@"(?<group>\w*\s?\d\d\d\d?/?\d*,?-?\d*)");

            var tables = mainData.Split(new[] { "</TABLE>" }, StringSplitOptions.RemoveEmptyEntries).Select(t =>
                  new Regex(@"</FONT><FONT FACE=.+>\s?(?<groupName>.+)</P>.*<TABLE .+ WIDTH=\d+>(?<table>.+)",
                      RegexOptions.Singleline).Match(t)).ToList();
            tables.RemoveAll(t => t.Groups["table"].Value == string.Empty);

            List<Schedule> preResult = new List<Schedule>();

            foreach (var rawTable in tables)
            {
                if (rawTable.Groups["groupName"].Value.Any(t => char.IsDigit(t) || char.IsLetter(t)))
                {
                    //==========================================================================
                    //формирование таблицы из монолитного кода==================================
                    var groupName = rawTable.Groups["groupName"].Value;
                    var linedTable = rawTable.Groups["table"].Value
                        .Split(new[] { "</TR>" }, StringSplitOptions.RemoveEmptyEntries).Select(t =>
                              new Regex(@"<TR>(?<line>.+)", RegexOptions.Singleline).Match(t).Groups["line"].Value)
                        .ToList();

                    linedTable.RemoveRange(0, 2);
                    linedTable.RemoveAll(t => t == String.Empty);

                    var tableOfNodes = linedTable
                        .Select(
                            t => new Regex(@"<P ALIGN=""CENTER"">_?\s*(?<line>.+)</FONT>", RegexOptions.IgnoreCase)
                                .Matches(t)
                                .Cast<Match>()
                                .Select(s => s.Groups["line"].Value).ToList()
                        ).ToList();
                    //==========================================================================

                    var schedulesList = new List<Schedule.ScheduleTable>();



                    for (int weekIndex = 0; weekIndex < tableOfNodes.Count / 6; weekIndex++)
                    {
                        //настройка смещения для разделение таблицы на подтаблицы по неделям
                        int mult = 7;
                        if (tableOfNodes.Count / 6 > 1) mult = 6;
                        //======================================

                        var formedScheduleTable = new Schedule.ScheduleTable
                        { SelectedWeek = tableOfNodes.Count / 6 > 1 ? weekIndex < 2 ? (Week)weekIndex : Week.Unidentified : Week.Unidentified };

                        for (var i = weekIndex * mult; i < (weekIndex + 1) * mult; i++)
                        {
                            var lineOfNodes = tableOfNodes[i];
                            for (int j = 1; j < lineOfNodes.Count; j++)
                            {
                                var lesson = LessonType.Default;
                                if (lineOfNodes[j].ToLower().Contains("лек.")) lesson = LessonType.Lection;
                                if (lineOfNodes[j].ToLower().Contains("пр.")) lesson = LessonType.Practice;
                                if (lineOfNodes[j].ToLower().Contains("лаб.")) lesson = LessonType.Laboratory;

                                var rawNode = lineOfNodes[j];
                                if (Auditory.Matches(lineOfNodes[j]).Count > 0)
                                {
                                    switch (type)
                                    {
                                        case NodeType.Student:
                                            {
                                                string prevSubject = String.Empty;
                                                do
                                                {
                                                    string curAuditory = new[]
                                                    {
                                                        new Regex(@"дист\.", RegexOptions.IgnoreCase),
                                                        new Regex(@"а\.\s?д\.\s?о\s?", RegexOptions.IgnoreCase)
                                                    }
                                                        .Select(reg => reg.Match(rawNode).Value)
                                                        .FirstOrDefault(s=>!string.IsNullOrEmpty(s));

                                                    if (string.IsNullOrEmpty(curAuditory))
                                                        curAuditory = Auditory.Match(rawNode).Groups["audit"].Value;
                                                    
                                                    var curProfessor = ProfessorFio.Match(rawNode).Value;

                                                    var spitted = MySplit(rawNode, curAuditory).ToArray();

                                                    rawNode = spitted.Length > 1 ? spitted[1] : string.Empty;

                                                    var curSubject = spitted[0];

                                                    if (curProfessor != string.Empty)
                                                        curSubject = curSubject.Replace(curProfessor, "");

                                                    var wrongMatches = ProfessorFio.Matches(curSubject).Cast<Match>().ToList();
                                                    if (wrongMatches.Any())
                                                    {
                                                        rawNode = string.Join(" ", wrongMatches.Select(t => t.Value)) + " " + rawNode;
                                                        curSubject = string.Empty;
                                                    }

                                                    if (curSubject.ToLower().Contains("вакансия"))
                                                        curSubject = curSubject.Remove(curSubject.ToLower().IndexOf("вакансия", StringComparison.Ordinal), 8);

                                                    if (curSubject != string.Empty)
                                                    {
                                                        curSubject = curSubject
                                                        .Replace("лек.", "")
                                                        .Replace("пр.", "")
                                                        .Replace("лаб.", "")
                                                        .Replace("а.", "")
                                                        .Replace("и/д", "")
                                                        .Replace(".", "")
                                                        .Replace(",", "")
                                                        .Replace("-", "");
                                                        
                                                        //if (curSubject != string.Empty)
                                                        while ("., ".Contains(curSubject[curSubject.Length - 1]))
                                                        {
                                                            curSubject = curSubject.Remove(curSubject.Length - 1, 1);
                                                            if (curSubject.Length == 0) break;
                                                        }

                                                        if (curSubject.Length != 0)
                                                            while (curSubject[0] == ' ')
                                                                curSubject = curSubject.Remove(0, 1);

                                                        if (curSubject != string.Empty)
                                                            prevSubject = curSubject;
                                                    }

                                                    while (curAuditory.Length > 0)
                                                    {
                                                        if (curAuditory[curAuditory.Length - 1] == ' ' ||
                                                            curAuditory[curAuditory.Length - 1] == '-' ||
                                                            curAuditory[curAuditory.Length - 1] == '.')
                                                            curAuditory = curAuditory.Remove(curAuditory.Length - 1, 1);
                                                        else break;
                                                    }

                                                    formedScheduleTable
                                                        .LectionList
                                                        .Add(new StNode(
                                                            (DayOfWeek)typeof(DayOfWeek).GetEnumValues()
                                                                .GetValue(i % mult),
                                                            (WorkingTime)typeof(WorkingTime).GetEnumValues()
                                                                .GetValue((j - 1) % mult),
                                                            lesson)
                                                        {
                                                            AuditoryName = curAuditory,
                                                            ProfessorName = CorrectRegister(curProfessor),
                                                            Subject = prevSubject
                                                        });
                                                } while (Auditory.Matches(rawNode).Count > 0);

                                            }
                                            break;
                                        case NodeType.Teacher:
                                        {
                                            var tempnode = rawNode;

                                                rawNode = rawNode
                                                    .Replace("лек.", "")
                                                    .Replace("пр.", "")
                                                    .Replace("лаб.", "")
                                                    .Replace("и/д", "");

                                                var curAuditory = Auditory.Match(rawNode).Groups["audit"].Value;
                                                if(!string.IsNullOrEmpty(curAuditory))
                                                    rawNode = rawNode.Replace(curAuditory, "");

                                                var curGroup = Group.Match(rawNode).Groups["group"].Value;
                                                if(!string.IsNullOrEmpty(curGroup))
                                                    rawNode = rawNode.Replace(curGroup, "");

                                                rawNode = rawNode
                                                    .Replace("а.", "")
                                                    .Replace(".", " ")
                                                    .Replace("-", " ");

                                                while (rawNode[rawNode.Length - 1] == ' ')
                                                {
                                                    rawNode = rawNode.Remove(rawNode.Length - 1, 1);
                                                    if (rawNode.Length == 0) break;
                                                }

                                                if (rawNode.Length > 0)
                                                    while (rawNode[0] == ' ')
                                                        rawNode = rawNode.Remove(0, 1);

                                                formedScheduleTable.LectionList
                                                    .Add(new PrepNode(
                                                        (DayOfWeek)typeof(DayOfWeek).GetEnumValues()
                                                            .GetValue(i % mult),
                                                        (WorkingTime)typeof(WorkingTime).GetEnumValues()
                                                            .GetValue((j - 1) % mult),
                                                        lesson)
                                                    {
                                                        AuditoryName = curAuditory,
                                                        GroupName = curGroup,
                                                        Subject = rawNode
                                                    });
                                            }
                                            break;
                                    }
                                }
                            }
                        }

                        //todo для отладки
                        //var t = formedScheduleTable.LectionList.Select(ss => ss.LectureHall + " | " + ((ss.GetType() == typeof(PrepNode)) ? ((PrepNode)ss).GroupName : ((StNode)ss).ProfessorName) + " | " + ss.Subject).ToList();

                        schedulesList.Add(formedScheduleTable);
                    }

                    if (preResult.Any(t => t.Name == groupName))
                    {
                        preResult.Find(t => t.Name == groupName).TablesList.AddRange(schedulesList);
                    }
                    else
                    {
                        var tempSh = new Schedule(groupName, schedulesList);

                        preResult.Add(tempSh);
                        //inputFunctionDelegate?.Invoke(tempSh);
                    }
                }
            }

            return preResult;
        }
    }
}