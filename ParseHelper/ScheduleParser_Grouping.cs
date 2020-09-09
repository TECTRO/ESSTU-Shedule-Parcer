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

        }
    }
}