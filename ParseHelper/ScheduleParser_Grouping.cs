using System;
using System.Collections.Generic;
using System.Linq;

namespace ParseHelper
{
    public partial class ScheduleParser
    {
        public readonly GroupingMethods Grouping;
        public class GroupingMethods
        {
            private readonly ScheduleParser _parent;

            internal GroupingMethods(ScheduleParser parent) => _parent = parent;
            //===============================================================================================

            /// <summary>
            /// перестраивает входную ноду в дерево по фильтру
            /// </summary>
            /// <param name="root">
            /// текущая нода
            /// </param>
            /// 
            /// <param name="filters">
            /// фильтр (фильтр по умолчанию есть в статик классе ноды)
            /// </param>
            public void CreateSubgroupsTree(GroupOfSchedule root, GroupOfSchedule.GroupFilter filters)
            {
                if (filters.Filter != null)
                    if (root.Schedules != null)
                        if (root.Schedules.Any(t => filters.Filter.IsMatch(t.Name) && t.GetNodeType() == filters.AssignType))
                            root = root.CreateSubgroup(filters.GroupName, filters.Filter, filters.AssignType);

                if (filters.SubFilters != null)
                    foreach (var filtersSubFilter in filters.SubFilters)
                    {
                        CreateSubgroupsTree(root, filtersSubFilter);
                    }
            }

            public void RemoveGarbageFromTree(GroupOfSchedule root)
            {
                if (root.Subgroups != null)
                {
                    if (root.Subgroups.Count != 0)
                        root.Schedules?.Clear();

                    foreach (var groupOfSchedule in root.Subgroups)
                        RemoveGarbageFromTree(groupOfSchedule);
                }
            }

            public GroupOfSchedule DefaultLoadSchedules(IEnumerable<LoadingStruct> loadingStructs)
            {
                var allSources = new List<PreLoadedStruct>();

                foreach (var loadingStruct in loadingStructs)
                {

                    foreach (var structSource in loadingStruct.Sources)
                    {
                        var foundedBySource =  allSources.FirstOrDefault(t => t.Sources.Any(s => s.WebLink == structSource.WebLink));

                        if (foundedBySource != null)
                        {
                            if (!foundedBySource.ConvertToType.Contains(loadingStruct.ConvertToType))
                                foundedBySource.ConvertToType.Add(loadingStruct.ConvertToType);
                        }
                        else
                        {
                            var foundedByType = allSources.FirstOrDefault(t => t.ConvertToType.Contains(loadingStruct.ConvertToType));
                            
                            if (foundedByType != null)
                            {
                                foundedByType.Sources.Add(structSource);
                            }
                            else

                                allSources.Add(
                                new PreLoadedStruct
                                {
                                    ConvertToType = new List<NodeType>
                                    {
                                        loadingStruct.ConvertToType
                                    }, 
                                    Sources = new List<GroupOfSchedule.Source>
                                    {
                                        structSource
                                    },
                                    LoadedSchedules = new List<Schedule>()
                                });
                        }
                    }
                }
                _parent.ThManager.Wait(() =>
                {
                    foreach (var preLoadedStruct in allSources)
                    {
                        foreach (var source in preLoadedStruct.Sources)
                        {
                            _parent.Async.LoadSchedulesRecurcieveAsync(source.WebLink, source.LinkType, preLoadedStruct.LoadedSchedules);
                        }
                    }
                });

                List<Schedule> unGroupedResult = new List<Schedule>();

                foreach (var preLoadedStruct in allSources)
                {
                    foreach (var convertToType in preLoadedStruct.ConvertToType)
                    {
                        preLoadedStruct.LoadedSchedules = _parent.Common.RemoveRepeats(preLoadedStruct.LoadedSchedules).ToList();

                        if(preLoadedStruct.LoadedSchedules.GetNodeTypes().ToList().Count>1)
                            throw new ArgumentException("неправильно сформирована структура ссылок на расписания! проверьте приводимые типы");

                        if (convertToType != preLoadedStruct.Sources.FirstOrDefault().LinkType)
                        {
                            if (convertToType == NodeType.Auditory)
                                unGroupedResult.AddRange(_parent.Sync.ConvertSchedulesToAuditoriums(preLoadedStruct.LoadedSchedules));
                        }
                        else
                            unGroupedResult.AddRange(preLoadedStruct.LoadedSchedules);
                    }
                }

                GroupOfSchedule result = new GroupOfSchedule(unGroupedResult);

                _parent.Grouping.CreateSubgroupsTree(result,GroupOfSchedule.DefaultFilters);

                _parent.Grouping.RemoveGarbageFromTree(result);

                return result;
            }

            private class PreLoadedStruct
            {
                public List<NodeType> ConvertToType { get; set; }
                public List<GroupOfSchedule.Source> Sources { get; set; }
                public List<Schedule> LoadedSchedules { get; set; }
            }


            public struct LoadingStruct
            {
                public NodeType ConvertToType { get; set; }
                public List<GroupOfSchedule.Source> Sources { get; set; }

                public LoadingStruct(NodeType convertToType, List<GroupOfSchedule.Source> sources)
                {
                    ConvertToType = convertToType;
                    Sources = sources;
                }
            }

            public static IEnumerable<LoadingStruct> DefaultLoadingStructs { get; }

            static GroupingMethods() 
            {
                DefaultLoadingStructs = new List<LoadingStruct>
                {
                    new LoadingStruct(NodeType.Student, new List<GroupOfSchedule.Source>
                    {
                        new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/raspisan.htm", NodeType.Student),
                        new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/raspisan.htm", NodeType.Student)
                    }),
                    new LoadingStruct(NodeType.Auditory, new List<GroupOfSchedule.Source>
                    {
                        new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/raspisan.htm", NodeType.Student),
                        new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/raspisan.htm", NodeType.Student)
                    }),
                    new LoadingStruct(NodeType.Teacher, new List<GroupOfSchedule.Source>
                    {
                        new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/craspisanEdt.htm", NodeType.Teacher),
                        new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/craspisanEdt.htm", NodeType.Teacher)
                    })
                };
            }
        }
    }
}