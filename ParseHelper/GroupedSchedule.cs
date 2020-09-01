﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ParseHelper
{
    /// <summary>
    /// Главный контейнер для обработки расписаний
    /// </summary>
    public class GroupOfSchedule
    {
        public struct Source
        {
            public string WebLink { get; set; }
            public NodeType LinkType { get; set; }
            public Source(string webLink, NodeType linkType)
            {
                WebLink = webLink;
                LinkType = linkType;
            }
        }
        public class GroupFilter
        {
            public GroupFilter this[string name]
            {
                get { return SubFilters?.FirstOrDefault(t => string.Equals(t.GroupName, name, StringComparison.CurrentCultureIgnoreCase)); }
            }
            public GroupFilter(string groupName, Regex filter, NodeType assignType, IEnumerable<GroupFilter> subFilters)
            {
                GroupName = groupName;
                Filter = filter;
                AssignType = assignType;
                SubFilters = subFilters?.ToList();
            }
            public List<GroupFilter> SubFilters { get; }
            public string GroupName { get; }
            public Regex Filter { get; }

            public NodeType AssignType { get; }
        }

        private readonly ThreadManager _thManager;
        private readonly ScheduleParser _helper;


        public GroupOfSchedule(IEnumerable<Schedule> schedules)
        {
            Schedules = schedules.ToList();

            _thManager = new ThreadManager();
            _helper = new ScheduleParser(_thManager);
        }
        public GroupOfSchedule()
        {
            _thManager = new ThreadManager();
            _helper = new ScheduleParser(_thManager);
        }
        public string GroupName { get; set; }
        public List<Schedule> Schedules { get; private set; }
        public List<GroupOfSchedule> Subgroups { get; private set; }
        public GroupOfSchedule this[string name]
        {
            get
            {
                return Subgroups?.FirstOrDefault(t => string.Equals(t.GroupName, name, StringComparison.CurrentCultureIgnoreCase));
            }
        }

        public void ToSubgroup(string subName, Regex filter, NodeType assignedType) => CreateSubgroup(subName, filter, assignedType);
        private GroupOfSchedule CreateSubgroup(string subName, Regex filter, NodeType assignedType)
        {
            if (Subgroups == null) Subgroups = new List<GroupOfSchedule>();

            var currentGroup = Subgroups.FirstOrDefault(t => string.Equals(t.GroupName, subName, StringComparison.CurrentCultureIgnoreCase));

            if (currentGroup != null)
                currentGroup.Schedules.AddRange(Schedules.Where(t => filter.IsMatch(t.Name) && t.GetNodeType() == assignedType));
            else
            {
                currentGroup = new GroupOfSchedule(Schedules.Where(t => filter.IsMatch(t.Name) && t.GetNodeType() == assignedType)) { GroupName = subName };
                Subgroups.Add(currentGroup);
            }

            Schedules.RemoveAll(t => filter.IsMatch(t.Name) && t.GetNodeType() == assignedType);
            if (Schedules.Count == 0) Schedules = null;
            return currentGroup;
        }
        public void ToSubgroupsTree(GroupFilter filters) => CreateSubgroupsTree(this, filters);
        private static void CreateSubgroupsTree(GroupOfSchedule group, GroupFilter filters)
        {
            if (filters.Filter != null)
                if (group.Schedules != null)
                    if (group.Schedules.Any(t => filters.Filter.IsMatch(t.Name) && t.GetNodeType() == filters.AssignType))
                        group = group.CreateSubgroup(filters.GroupName, filters.Filter, filters.AssignType);

            if (filters.SubFilters != null)
                foreach (var filtersSubFilter in filters.SubFilters)
                {
                    CreateSubgroupsTree(group, filtersSubFilter);
                }
        }
        //todo убрать методы loadschedules u loadauditoriums из groupedSchedule в parseHelper скорее всего в синк раздел, и очистить этот раздел от старых и неэффективных методов, которые только затрудняют ориентирование внутри проекта
        public void LoadSchedules(IEnumerable<Source> sources)
        {
            var loadedSchedules = new List<Schedule>();

            _thManager.Wait(() =>
            {
                foreach (var source in sources)
                {
                    _helper.Async.FillTableRecurcieveAsync(source.WebLink, source.LinkType, loadedSchedules);
                }
            });

            if (Schedules == null) Schedules = new List<Schedule>();
            Schedules.AddRange(_helper.RemoveRepeats(loadedSchedules));
        }

        public void LoadAsAuditoriums(IEnumerable<Source> sources)
        {
            var loadedSchedules = new List<Schedule>();
            foreach (var source in sources)
            {
                var res = new List<Schedule>();
                _thManager.Wait(() => { _helper.Async.FillTableRecurcieveAsync(source.WebLink, source.LinkType, res); });
                loadedSchedules.AddRange(res);
            }
            //foreach (var source in sources)
            //{
            //    loadedSchedules.AddRange(_helper.FillTableRecurcieveAsyncAndWait(source.WebLink, source.LinkType));
            //}

            if (Schedules == null) Schedules = new List<Schedule>();
            Schedules.AddRange(_helper.Sync.ToAuditorySchedules(_helper.RemoveRepeats(loadedSchedules)));
        }
        public void ToAuditoriums(IEnumerable<Schedule> sources)
        {
            if (Schedules == null) Schedules = new List<Schedule>();
            Schedules.AddRange(_helper.Sync.ToAuditorySchedules(sources));
        }

        public ScheduleParser GetHelper()
        {
            return _helper;
        }
        /// <summary>
        /// Work In Progress
        /// </summary>
        public static GroupFilter DefaultFilters { get; }

        static GroupOfSchedule()
        {
            DefaultFilters = new GroupFilter("DEFAULT", null, NodeType.Error, new[]
            {
                new GroupFilter("Корпуса",new Regex(@"(?<audit>\d*-?\w*-?\w*)"),NodeType.Auditory, new []
                {
                    new GroupFilter("Корпус 1",new Regex("^1.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 2",new Regex("^2.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 3",new Regex("^3.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 4",new Regex("^4.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 5",new Regex("^5.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 6",new Regex("^6.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 7",new Regex("^7.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 8",new Regex("^8.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 9",new Regex("^9.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 10",new Regex("^0.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 11",new Regex("^11-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 12",new Regex("^12-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 13",new Regex("^13-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 14",new Regex("^14-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 15",new Regex("^15-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 16",new Regex("^16-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 17",new Regex("^17-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 18",new Regex("^18-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 19",new Regex("^19-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 20",new Regex("^20-.*"),NodeType.Auditory,null ),
                    new GroupFilter("Корпус 21",new Regex("^21-.*"),NodeType.Auditory,null )
                }),
                new GroupFilter("Студенты", ScheduleParser.Group,NodeType.Student, new []
                {
                    new GroupFilter("Бакалавриат", new Regex(@"\w*Б\w*\s?\d{3}.*",RegexOptions.IgnoreCase),NodeType.Student, null),
                    new GroupFilter("Специалитет", new Regex(@"^\d{3}.*"),NodeType.Student, null),
                    new GroupFilter("Магистратура", new Regex(@"^М\d{3}.*",RegexOptions.IgnoreCase),NodeType.Student, null),
                    new GroupFilter("Заочное", new Regex(@"^З.*",RegexOptions.IgnoreCase),NodeType.Student, null),
                    new GroupFilter("Колледж", new Regex(@"^К.*",RegexOptions.IgnoreCase),NodeType.Student, null)
                }),
                new GroupFilter("Преподаватели", new Regex(@".*"), NodeType.Teacher, null),
            });
        }
    }

    public class Schedule
    {
        public string Name { get; }
        public List<ScheduleTable> TablesList { get; }

        public Schedule(string name, IEnumerable<ScheduleTable> tables)
        {
            Name = name;
            TablesList = new List<ScheduleTable>(tables);
        }

        public NodeType GetNodeType()
        {
            var types = TablesList.Select(t => t.GetNodeType()).Where(t => t != NodeType.Error).ToList();
            return types.Any() ? types.FirstOrDefault() : NodeType.Error;
        }

        /// <summary>
        /// номер таблицы расписания -> столбец расписания -> строка расписания -> список ячеек расписания в этот день.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IEnumerable<IEnumerable<IEnumerable<Node>>>> GetAsTables()
        {
            return TablesList.Select(t => t.ConvertToTable());
        }

        public class ScheduleTable
        {
            public Week SelectedWeek { get; set; }
            public List<Node> LectionList { get; }
            public ScheduleTable()
            {
                LectionList = new List<Node>();
            }

            public IEnumerable<IEnumerable<IEnumerable<Node>>> ConvertToTable()
            {
                List<Node>[][] result = new List<Node>[typeof(WorkingTime).GetEnumValues().Length][];

                for (var i = 0; i < result.Length; i++)
                    result[i] = new List<Node>[typeof(DayOfWeek).GetEnumValues().Length];

                foreach (var node in LectionList)
                {
                    if (result[(int)node.Time][(int)node.Day] == null)
                        result[(int)node.Time][(int)node.Day] = new List<Node>();

                    result[(int)node.Time][(int)node.Day].Add(node);
                }

                return result;
            }

            public NodeType GetNodeType()
            {
                if (LectionList.Any(t => t is PrepNode))
                    return NodeType.Teacher;
                if (LectionList.Any(t => t is StNode))
                    return NodeType.Student;
                if (LectionList.Any(t => t is AuditoryNode))
                    return NodeType.Auditory;
                return NodeType.Error;
            }
        }
    }
}