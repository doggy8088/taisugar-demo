namespace PropertyManagement.Models;

public record InjuryBodyPart(string Id, string Name, string Group, string Side, string Surface,
    double X, double Y, double Z, double Width, double Height, double Depth, bool Overview = false,
    double Rotation = 0);
