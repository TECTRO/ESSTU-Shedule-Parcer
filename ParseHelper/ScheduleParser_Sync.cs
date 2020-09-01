using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace ParseHelper
{
    public partial class ScheduleParser
    {
        public readonly SyncMethods Sync;
        public class SyncMethods
        {
            private readonly ScheduleParser _parent;
            internal SyncMethods(ScheduleParser parent) => _parent = parent;
            //===============================================================================================
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
                        _parent.ExceptionEvent?.Invoke(e);
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
            public IEnumerable<Schedule> ToAuditorySchedules(IEnumerable<Schedule> source)
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

                    return _parent.FillTable(type, mainData);
                }
            }

        }
    }
}