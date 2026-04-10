namespace project_approval_system.Data;

public static class Roles
{
    public const string Student = "Student";
    public const string Supervisor = "Supervisor";
    public const string ModuleLeader = "ModuleLeader";

    public static readonly string[] All = [Student, Supervisor, ModuleLeader];
}
