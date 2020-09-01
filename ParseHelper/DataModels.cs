namespace ParseHelper
{
    /////CLASSES/////////////////////////////////////////
    public enum DayOfWeek
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday
    }

    public enum WorkingTime
    {
        First,
        Second,
        Third,
        Fourth,
        Fifth,
        Sixth,
        Seventh,
        Eighth
    }
    public enum Week
    {
        Uneven,
        Even,
        Unidentified
    }
    public enum LessonType
    {
        Default,

        Lection,
        Practice,
        Laboratory
    }

    public enum NodeType
    {
        Student,
        Teacher,
        Auditory,
        Error
    }

    public enum SearchLevel
    {
        InScheduleName,
        InNodesGroups,
        InNodesAuditoriums,
        InNodesProfessors
    }
    public class Node
    {
        public DayOfWeek Day { get; }
        public WorkingTime Time { get; }

        public LessonType LessonType { get; }
        public string Subject { get; set; }

        public override string ToString()
        {
            return (LessonType != LessonType.Default ? LessonType.ToString() + " \n" : "") + Subject;
        }
        public Node(DayOfWeek day, WorkingTime time, LessonType lType)
        {
            LessonType = lType;
            Day = day;
            Time = time;
        }
    }

    interface IStudentNode
    {
        string GroupName { get; set; }
    }
    interface IProfessorNode
    {
        string ProfessorName { get; set; }
    }
    interface IAuditoryNode
    {
        string AuditoryName { get; set; }
    }

    public class StNode : Node, IAuditoryNode, IProfessorNode
    {
        public string ProfessorName { get; set; }
        public string AuditoryName { get; set; }
        public override string ToString()
        {
            return base.ToString() + " \n" + ProfessorName + " \n" + AuditoryName;
        }
        public StNode(DayOfWeek day, WorkingTime time, LessonType lType) : base(day, time, lType) { }
    }
    public class PrepNode : Node, IAuditoryNode, IStudentNode
    {
        public string AuditoryName { get; set; }
        public string GroupName { get; set; }

        public override string ToString()
        {
            return base.ToString() + " \n" + AuditoryName + " \n" + GroupName;
        }
        public PrepNode(DayOfWeek day, WorkingTime time, LessonType lType) : base(day, time, lType) { }
    }

    public class AuditoryNode : Node, IStudentNode, IProfessorNode
    {
        public string GroupName { get; set; }
        public string ProfessorName { get; set; }
        public override string ToString()
        {
            return base.ToString() + " \n" + ProfessorName + " \n" + GroupName;
        }
        public AuditoryNode(DayOfWeek day, WorkingTime time, LessonType lType) : base(day, time, lType) { }
    }


}