namespace Omnijoy.Core.Models.Enums;

public enum FamilyRelationSubtype
{
    // Parent subtypes
    Mother = 0,
    Father = 1,
    MotherInLaw = 2,
    FatherInLaw = 3,
    StepMother = 4,
    StepFather = 5,

    // Sibling subtypes
    Sister = 10,
    Brother = 11,
    HalfSister = 12,
    HalfBrother = 13,
    StepSister = 14,
    StepBrother = 15,
    Cousin = 16,

    // Child subtypes
    Daughter = 20,
    Son = 21,
    StepDaughter = 22,
    StepSon = 23
}
