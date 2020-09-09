using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ParseHelper
{
    public partial class ScheduleParser
    {
        public readonly CommonMethods Common;
        public class CommonMethods
        {
            private readonly ScheduleParser _parent;

            internal CommonMethods(ScheduleParser parent) => _parent = parent;
            //===============================================================================================
            public IEnumerable<Schedule> TryFilterSchedules(IEnumerable<Schedule> tables, Regex regExFilter, SearchLevel level)
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
        }
    }
}