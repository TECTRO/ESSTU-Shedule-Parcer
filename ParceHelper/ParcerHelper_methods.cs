using System;
using System.Collections.Generic;
using System.Data.Metadata.Edm;
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


// ReSharper disable once CheckNamespace
namespace ParseHelper
{
    public partial class ParserHelper
    {
        public delegate void GerLinksDelegate(string tableLink);

        ///REGEX-ES//////////////////////////////////////////////////////////
        protected static Regex WebLinkMask = new Regex(@"(?<mask>\S+)/\S+\.\S+", RegexOptions.IgnoreCase);
        protected static Regex WebLinkAddress = new Regex(@"href=""(?<address>\S+\.\S+)"".*<FONT.+>(?<linkname>.+)</FONT>", RegexOptions.IgnoreCase);
        protected static Regex WebLinkAddressShort = new Regex(@"href=""(?<address>\S+\.\S+)""",RegexOptions.IgnoreCase);

        public static readonly Regex Auditory = new Regex(@"а\.(?<audit>\d*-?\w*-?\w*)"); //@"\d?\d?-?\d{2}?\d?-?\d?"
        public static readonly Regex ProfessorFio = new Regex(@"\w+\s\w[\.\,]\s?\w[\.\,]?");
        public static readonly Regex Group = new Regex(@"(?<group>\w*\s?\d\d\d?\d?/?\d*,?-?\d*)");
        /////////////////////////////////////////////////////////////////////


        ///synchronization plug for async recursive methods
        private readonly object _synchronizationPlug = new object();
        ///
        
        private ThreadManager ThManager { get; } 
        public ParserHelper(ThreadManager manager) => ThManager = manager;

        private static TextInfo _info;
        public string CorrectRegister(string source)
        {
            if (_info==null) _info = new CultureInfo("en-US", false).TextInfo;
            
            return _info.ToTitleCase(source.ToLower());
        }

        public IEnumerable<string> GetLinksRecursive(string website)
        {
            string mask = WebLinkMask.Match(website).Groups["mask"].Value + '/'; 

            using (WebClient client = new WebClient())
            {
                string mainData;

                try
                {
                    mainData = client.DownloadString(website);
                }
                catch (Exception e)
                {
                    ExceptionEvent?.Invoke(e);
                    return null;
                }

                List<string> results = new List<string>();

                var filteredMatches = WebLinkAddress.Matches(mainData).Cast<Match>()
                    .Where(t => !t.Groups["address"].Value.Contains("http") &&
                                t.Groups["linkname"].Value.Any(c => char.IsDigit(c) || char.IsLetter(c)))
                    .Select(t => t.Groups["address"].Value)
                    .Distinct()
                    .ToList();

                if (!filteredMatches.Any())
                {
                    filteredMatches = WebLinkAddressShort.Matches(mainData).Cast<Match>()
                        .Where(t => !t.Groups["address"].Value.Contains("http"))
                        .Select(t => t.Groups["address"].Value)
                        .Distinct()
                        .ToList();
                }

                if (filteredMatches.Any())
                {
                    foreach (var match in filteredMatches)
                    {
                        results.AddRange(GetLinksRecursive(mask + match));
                    }
                }
                else results.Add(website);

                return results;
            }
        }

        public delegate void ExceptionDelegate(Exception exc);

        public event ExceptionDelegate ExceptionEvent;

        public IEnumerable<Schedule> RemoveRepeats(IEnumerable<Schedule> schedules)
        {
            var converted = schedules.ToList();
            for (int i = 0; i < converted.Count; i++)
            {
                for (int j = i + 1; j < converted.Count; j++)
                {
                    if (converted[i].Name == converted[j].Name)
                    {
                        for (int k = 0; k < converted[i].TablesList.Count; k++)
                        {
                            for (int l = 0; l < converted[j].TablesList.Count; l++)
                            {
                                if (converted[j].TablesList[l].SelectedWeek != Week.Unidentified)
                                    if (converted[i].TablesList[k].SelectedWeek == converted[j].TablesList[l].SelectedWeek)
                                    {
                                        converted[i].TablesList[k].LectionList.AddRange(converted[j].TablesList[l].LectionList);
                                        converted[j].TablesList[l].LectionList.Clear();
                                        converted[j].TablesList.Remove(converted[j].TablesList[l]);
                                        l--;
                                    }
                            }
                        }

                        if (converted[j].TablesList.Any())
                        {
                            converted[i].TablesList.AddRange(converted[j].TablesList);
                            converted[j].TablesList.Clear();
                        }

                        converted.Remove(converted[j]);
                        j--;
                    }
                }
            }

            return converted;
        }
        private void GetLinksRecursiveAsync(string website, ICollection<string> result, GerLinksDelegate inputFunctionDelegate)
        {
            string mask = WebLinkMask.Match(website).Groups["mask"].Value + '/';

            using (WebClient client = new WebClient())
            {
                string mainData;

                try
                {
                    mainData = client.DownloadString(website);
                }
                catch (Exception e)
                {
                    ExceptionEvent?.Invoke(e);
                    return;
                }

                var filteredMatches = WebLinkAddress.Matches(mainData).Cast<Match>()
                    .Where(t => !t.Groups["address"].Value.Contains("http") && t.Groups["linkname"].Value
                                    .Any(c => char.IsDigit(c) || char.IsLetter(c)))
                    .Select(t => t.Groups["address"].Value)
                    .Distinct()
                    .ToList();

                if (!filteredMatches.Any())
                {
                    filteredMatches = WebLinkAddressShort.Matches(mainData).Cast<Match>()
                        .Where(t => !t.Groups["address"].Value.Contains("http"))
                        .Select(t => t.Groups["address"].Value)
                        .Distinct()
                        .ToList();
                }

                if (filteredMatches.Any())
                {
                    foreach (var match in filteredMatches)
                    {
                        ThManager.Add(
                            new Thread(() =>
                        {
                            GetLinksRecursiveAsync(mask + match, result, inputFunctionDelegate);

                        }));
                    }
                }
                else
                {
                    lock (_synchronizationPlug)
                    {
                        result?.Add(website);
                    }

                    inputFunctionDelegate?.Invoke(website);
                }
            }
        }

        public void GetLinksRecursiveAsync(string website, ICollection<string> result)
        {
            GetLinksRecursiveAsync(website, result, null);
        }

        public void FillTableRecurcieveAsync(string website, NodeType type, ICollection<Schedule> result)
        {
            GetLinksRecursiveAsync(website, null, e =>
             {
                 FillTableAsync(e, type, result);
             });
        }

        public void FillTableRecurcieveAsync(IEnumerable<string> websites, NodeType type, ICollection<Schedule> result)
        {
            foreach (var website in websites)
            {
                var curThread = new Thread(() => {
                    GetLinksRecursiveAsync(website, null, e =>
                    {
                        FillTableAsync(e, type, result);
                    });
                });
                
                ThManager.Add(curThread);
            }
        }
        public IEnumerable<Schedule> SelectTables(IEnumerable<Schedule> tables, Regex regExFilter, SearchLevel level)
        {
            switch (level)
            {
                case SearchLevel.InScheduleName:
                    {
                        return tables.Where(t => regExFilter.Matches(t.Name).Count > 0);
                    }
                case SearchLevel.InNodesAuditoriums:
                    {
                        return tables.Where(t => t.TablesList.Any(s => s.LectionList.Any(f => f.GetType() == typeof(IAuditoryNode) && regExFilter.Matches(((IAuditoryNode)f).AuditoryName).Count > 0)));
                    }
                case SearchLevel.InNodesGroups:
                    {
                        return tables.Where(t => t.TablesList.Any(s => s.LectionList.Any(f => f.GetType() == typeof(IStudentNode) && regExFilter.Matches(((IStudentNode)f).GroupName).Count > 0)));
                    }
                case SearchLevel.InNodesProfessors:
                    {
                        return tables.Where(t => t.TablesList.Any(s => s.LectionList.Any(f => f.GetType() == typeof(IProfessorNode) && regExFilter.Matches(((IProfessorNode)f).ProfessorName).Count > 0)));
                    }

                default: return new List<Schedule>();
            }

        }
        public IEnumerable<Schedule> ConvertToAuditorySchedule(IEnumerable<Schedule> source)
        {
            //internal methods ===============================================

            AuditoryNode GetConvertedNode(Schedule origSchedule, Node origNode)
            {
                var newAuditoryNode = new AuditoryNode(origNode.Day, origNode.Time, origNode.LessonType) { Subject = origNode.Subject };

                switch (origSchedule.GetNodeType())
                {
                    case NodeType.Student:
                        {
                            newAuditoryNode.GroupName = origSchedule.Name;
                            newAuditoryNode.ProfessorName = (origNode as IProfessorNode)?.ProfessorName;
                        }
                        break;

                    case NodeType.Teacher:
                        {
                            newAuditoryNode.GroupName = (origNode as IStudentNode)?.GroupName;
                            newAuditoryNode.ProfessorName = origSchedule.Name;
                        }
                        break;
                }

                return newAuditoryNode;
            }

            Schedule.ScheduleTable GetConvertedScheduleTable(Schedule origSchedule, Schedule.ScheduleTable origScheduleTable, Node origNode)
            {
                Schedule.ScheduleTable audTable = new Schedule.ScheduleTable { SelectedWeek = origScheduleTable.SelectedWeek };

                audTable.LectionList.Add(GetConvertedNode(origSchedule, origNode));

                return audTable;
            }

            Schedule GetConvertedSchedule(Schedule origSchedule, Schedule.ScheduleTable origScheduleTable, Node origNode)
            {
                return new Schedule(((IAuditoryNode)origNode).AuditoryName, new[] { GetConvertedScheduleTable(origSchedule, origScheduleTable, origNode) });
            }
            //=================================================================

            List<Schedule> result = new List<Schedule>();

            foreach (var schedule in source)
            {
                foreach (var scheduleTable in schedule.TablesList)
                {
                    foreach (var node in scheduleTable.LectionList)
                    {
                        if (!result.Exists(t => t.Name == ((IAuditoryNode)node).AuditoryName))
                        {
                            result.Add(GetConvertedSchedule(schedule, scheduleTable, node));
                        }
                        else
                        {
                            var curAudSchedule = result.Find(t => t.Name == ((IAuditoryNode)node).AuditoryName);

                            if (!curAudSchedule.TablesList.Exists(t => t.SelectedWeek == scheduleTable.SelectedWeek))
                            {
                                curAudSchedule.TablesList.Add(GetConvertedScheduleTable(schedule, scheduleTable, node));
                            }
                            else
                            {
                                var curAudTable = curAudSchedule.TablesList.Find(t => t.SelectedWeek == scheduleTable.SelectedWeek);

                                curAudTable.LectionList.Add(GetConvertedNode(schedule, node));
                            }
                        }
                    }
                }
            }
            return result;
        }
        public IEnumerable<Schedule> FillTableRecurcieve(string website, NodeType type)
        {
            return FillTable(GetLinksRecursive(website), type);
        }
        public IEnumerable<Schedule> FillTableRecurcieve(IEnumerable<string> websites, NodeType type)
        {
            List<string> links = new List<string>();
            foreach (var website in websites)
            {
                links.AddRange(GetLinksRecursive(website));
            }
            return FillTable(links, type);
        }
        public IEnumerable<Schedule> FillTable(IEnumerable<string> tableLinks, NodeType type)
        {
            var result = new List<Schedule>();

            foreach (var link in tableLinks)
                result.AddRange(FillTable(link, type));

            return result;
        }
        public IEnumerable<Schedule> FillTable(string tableLink, NodeType type)
        {
            using (WebClient client = new WebClient())
            {
                string mainData;

                try
                {
                    mainData = client.DownloadString(tableLink);
                }
                catch
                {
                    return new List<Schedule>();
                }

                return FillTable(type, mainData);
            }
        }

        private void FillTableAsync(string tableLink, NodeType type, ICollection<Schedule> result)
        {
            Thread local = new Thread(() =>
            {
                using (WebClient client = new WebClient())
                {
                    try
                    {
                        var preResult = FillTable(type, client.DownloadString(tableLink));

                        lock (_synchronizationPlug)
                        {
                            foreach (var schedule in preResult)
                                result.Add(schedule);
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionEvent?.Invoke(e);
                    }
                }
            });
            ThManager.Add(local);
        }

        public delegate void WaiterDelegate();
        public void AsyncWaiter(WaiterDelegate waiterFunc)
        {
            bool isFinished = false;
            void FinishMarker()
            {
                isFinished = true;
            }
            ThManager.ThreadsEndedEvent += FinishMarker;

            waiterFunc?.Invoke();

            while (!isFinished) { }
            ThManager.ThreadsEndedEvent -= FinishMarker;

        }
        private IEnumerable<string> MySplit(string value,string splitter)
        {
            List<string> result = new List<string>();

            var splitVal = value.IndexOf(splitter, StringComparison.Ordinal);

            result.Add(value.Substring(0,splitVal));
            result.Add(value.Remove(0, splitVal + splitter.Length));
            return result;
        }

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
                                                    var curAuditory = Auditory.Match(rawNode).Groups["audit"].Value;
                                                    var curProfessor = ProfessorFio.Match(rawNode).Value;

                                                    var spitted = MySplit(rawNode, curAuditory).ToArray();

                                                    rawNode = spitted.Length > 1 ? spitted[1] : string.Empty;

                                                    var curSubject = spitted[0];

                                                    if (curProfessor != string.Empty)
                                                        curSubject = curSubject.Replace(curProfessor, "");

                                                    var wrongMatches = ProfessorFio.Matches(curSubject).Cast<Match>().ToList();
                                                    if (wrongMatches.Any())
                                                    {
                                                        rawNode = string.Join(" ", wrongMatches.Select(t=>t.Value))+" "+ rawNode;
                                                        curSubject = string.Empty;
                                                    }

                                                    if (curSubject.ToLower().Contains("вакансия"))
                                                        curSubject = curSubject.Remove(curSubject.ToLower().IndexOf("вакансия", StringComparison.Ordinal),8);

                                                    if (curSubject != string.Empty)
                                                    {
                                                        curSubject = curSubject
                                                        .Replace("лек.", "")
                                                        .Replace("пр.", "")
                                                        .Replace("лаб.", "")
                                                        .Replace("а.", "")
                                                        .Replace("и/д", "")
                                                        .Replace(".", "")
                                                        .Replace("-", "");

                                                        while (curSubject[curSubject.Length - 1] == ' ')
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

                                                    while (curAuditory.Length>0)
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
                                                rawNode = rawNode
                                                    .Replace("лек.", "")
                                                    .Replace("пр.", "")
                                                    .Replace("лаб.", "")
                                                    .Replace("и/д", "");

                                                var curAuditory = Auditory.Match(rawNode).Groups["audit"].Value;
                                                rawNode = rawNode.Replace(curAuditory, "");

                                                var curGroup = Group.Match(rawNode).Groups["group"].Value;
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
