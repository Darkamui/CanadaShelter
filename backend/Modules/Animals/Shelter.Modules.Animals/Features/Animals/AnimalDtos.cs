using Microsoft.AspNetCore.Mvc;
using Shelter.Modules.Animals.Domain;

namespace Shelter.Modules.Animals.Features.Animals;

/// <summary>Filters, search and page of the animal list.</summary>
/// <param name="Page">1-based page; default 1.</param>
/// <param name="PageSize">1 to 100; default 25.</param>
/// <param name="Q">Name in part, number (with or without <c>#</c>), or an active microchip or other identifier.</param>
/// <param name="Status">Custody status: <c>not_in_care</c>, <c>in_care</c> or <c>outcome</c>.</param>
/// <param name="Species">Species code.</param>
/// <param name="LocationId">Animals at this location or below it.</param>
/// <param name="Sort"><c>-number</c> (default, newest first), <c>number</c>, <c>name</c> or <c>-name</c>.</param>
internal sealed record ListAnimalsQuery(
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "pageSize")] int? PageSize,
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "species")] string? Species,
    [FromQuery(Name = "locationId")] Guid? LocationId,
    [FromQuery(Name = "sort")] string? Sort);

/// <summary>Register an animal. Do not put personal information in the alert or marks texts.</summary>
/// <param name="Name">Name; the app shows the number when there is none.</param>
/// <param name="SpeciesCode">Code of the species list.</param>
/// <param name="Breed">Breed, plain text.</param>
/// <param name="SecondaryBreed">Second breed of a cross, plain text.</param>
/// <param name="Colour">Colour, plain text.</param>
/// <param name="Sex"><c>unknown</c> (default), <c>male</c> or <c>female</c>.</param>
/// <param name="ReproductiveStatus"><c>unknown</c> (default), <c>intact</c> or <c>sterilized</c>.</param>
/// <param name="BirthDate">Birth date.</param>
/// <param name="BirthDateEstimated">Whether the birth date is an estimate.</param>
/// <param name="Marks">Distinctive marks.</param>
/// <param name="BehaviourAlert">Behaviour alert shown to staff.</param>
/// <param name="MedicalAlert">Medical alert shown to staff.</param>
/// <param name="LegalAlert">Legal alert (hold, seizure, court order) shown to staff.</param>
/// <param name="Microchip">Microchip number, if known.</param>
internal sealed record CreateAnimalRequest(
    string? Name,
    string? SpeciesCode,
    string? Breed,
    string? SecondaryBreed,
    string? Colour,
    string? Sex,
    string? ReproductiveStatus,
    DateOnly? BirthDate,
    bool? BirthDateEstimated,
    string? Marks,
    string? BehaviourAlert,
    string? MedicalAlert,
    string? LegalAlert,
    string? Microchip) : IAnimalFields;

/// <summary>Replace an animal's descriptive fields. Identifiers and custody are changed elsewhere.</summary>
/// <param name="Name">Name.</param>
/// <param name="SpeciesCode">Code of the species list.</param>
/// <param name="Breed">Breed, plain text.</param>
/// <param name="SecondaryBreed">Second breed of a cross, plain text.</param>
/// <param name="Colour">Colour, plain text.</param>
/// <param name="Sex"><c>unknown</c>, <c>male</c> or <c>female</c>.</param>
/// <param name="ReproductiveStatus"><c>unknown</c>, <c>intact</c> or <c>sterilized</c>.</param>
/// <param name="BirthDate">Birth date.</param>
/// <param name="BirthDateEstimated">Whether the birth date is an estimate.</param>
/// <param name="Marks">Distinctive marks.</param>
/// <param name="BehaviourAlert">Behaviour alert.</param>
/// <param name="MedicalAlert">Medical alert.</param>
/// <param name="LegalAlert">Legal alert.</param>
/// <param name="Version">The <c>version</c> the client read; 409 when the animal changed since.</param>
internal sealed record UpdateAnimalRequest(
    string? Name,
    string? SpeciesCode,
    string? Breed,
    string? SecondaryBreed,
    string? Colour,
    string? Sex,
    string? ReproductiveStatus,
    DateOnly? BirthDate,
    bool? BirthDateEstimated,
    string? Marks,
    string? BehaviourAlert,
    string? MedicalAlert,
    string? LegalAlert,
    uint Version) : IAnimalFields;

/// <summary>Add an identifier.</summary>
/// <param name="Type"><c>microchip</c>, <c>licence</c> or <c>external</c>.</param>
/// <param name="Value">The number as written.</param>
internal sealed record AnimalIdentifierRequest(string? Type, string? Value);

/// <summary>A row of the animal list.</summary>
/// <param name="Id">Animal ID.</param>
/// <param name="Number">Per-organization number.</param>
/// <param name="Name">Name.</param>
/// <param name="SpeciesCode">Species code.</param>
/// <param name="Breed">Breed.</param>
/// <param name="Sex">Sex code.</param>
/// <param name="CustodyStatus">Custody status code.</param>
/// <param name="CurrentLocationId">Location while in care.</param>
/// <param name="CurrentLocationName">That location's name.</param>
/// <param name="InCareSince">Start of the current stay.</param>
/// <param name="HasAlerts">Whether a behaviour, medical or legal alert is set.</param>
internal sealed record AnimalListItem(
    Guid Id,
    int Number,
    string? Name,
    string SpeciesCode,
    string? Breed,
    string Sex,
    string CustodyStatus,
    Guid? CurrentLocationId,
    string? CurrentLocationName,
    DateTimeOffset? InCareSince,
    bool HasAlerts);

/// <summary>An animal with its identifiers and custody summary.</summary>
/// <param name="Id">Animal ID.</param>
/// <param name="Number">Per-organization number.</param>
/// <param name="Name">Name.</param>
/// <param name="SpeciesCode">Species code.</param>
/// <param name="Breed">Breed.</param>
/// <param name="SecondaryBreed">Second breed.</param>
/// <param name="Colour">Colour.</param>
/// <param name="Sex">Sex code.</param>
/// <param name="ReproductiveStatus">Reproductive status code.</param>
/// <param name="BirthDate">Birth date.</param>
/// <param name="BirthDateEstimated">Whether the birth date is an estimate.</param>
/// <param name="Marks">Distinctive marks.</param>
/// <param name="BehaviourAlert">Behaviour alert.</param>
/// <param name="MedicalAlert">Medical alert.</param>
/// <param name="LegalAlert">Legal alert.</param>
/// <param name="CustodyStatus">Custody status code.</param>
/// <param name="CurrentLocationId">Location while in care.</param>
/// <param name="CurrentLocationName">That location's name.</param>
/// <param name="InCareSince">Start of the current stay.</param>
/// <param name="LastOutcomeCode">Outcome type of the last outcome.</param>
/// <param name="Identifiers">Identifiers, active first.</param>
/// <param name="CreatedAt">When the animal was registered.</param>
/// <param name="UpdatedAt">When the record last changed.</param>
/// <param name="Version">Send back when updating.</param>
internal sealed record AnimalResponse(
    Guid Id,
    int Number,
    string? Name,
    string SpeciesCode,
    string? Breed,
    string? SecondaryBreed,
    string? Colour,
    string Sex,
    string ReproductiveStatus,
    DateOnly? BirthDate,
    bool BirthDateEstimated,
    string? Marks,
    string? BehaviourAlert,
    string? MedicalAlert,
    string? LegalAlert,
    string CustodyStatus,
    Guid? CurrentLocationId,
    string? CurrentLocationName,
    DateTimeOffset? InCareSince,
    string? LastOutcomeCode,
    IReadOnlyList<AnimalIdentifierItem> Identifiers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    public static AnimalResponse From(Animal a, string? locationName, IReadOnlyList<AnimalIdentifierItem> identifiers) => new(
        a.Id, a.Number, a.Name, a.SpeciesCode, a.Breed, a.SecondaryBreed, a.Colour, a.Sex, a.ReproductiveStatus, a.BirthDate,
        a.BirthDateEstimated, a.Marks, a.BehaviourAlert, a.MedicalAlert, a.LegalAlert, a.CustodyStatus, a.CurrentLocationId,
        locationName, a.InCareSince, a.LastOutcomeCode, identifiers, a.CreatedAt, a.UpdatedAt, a.Version);
}

/// <summary>An identifier of an animal.</summary>
/// <param name="Id">Identifier ID.</param>
/// <param name="Type"><c>microchip</c>, <c>licence</c> or <c>external</c>.</param>
/// <param name="Value">The number as written.</param>
/// <param name="IsActive">Whether it is still in use.</param>
/// <param name="CreatedAt">When it was added.</param>
/// <param name="DeactivatedAt">When it was deactivated.</param>
internal sealed record AnimalIdentifierItem(Guid Id, string Type, string Value, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? DeactivatedAt)
{
    public static AnimalIdentifierItem From(AnimalIdentifier i) => new(i.Id, i.Type, i.Value, i.IsActive, i.CreatedAt, i.DeactivatedAt);
}

/// <summary>A timeline event.</summary>
/// <param name="Id">Event ID.</param>
/// <param name="Type">Event type code, such as <c>animal_registered</c> or <c>intake_recorded</c>.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="RecordedAt">When it was recorded.</param>
/// <param name="SourceModule">Module that recorded it.</param>
/// <param name="SourceRecordId">ID of the record in that module.</param>
/// <param name="Parameters">Codes and IDs, by name.</param>
/// <param name="Names">Display names of the location and person IDs among <paramref name="Parameters"/>, by the same key.</param>
internal sealed record TimelineItem(
    Guid Id,
    string Type,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    string SourceModule,
    Guid? SourceRecordId,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, string> Names);

/// <summary>Animals in care at a location.</summary>
/// <param name="LocationId">Location ID.</param>
/// <param name="Count">Animals at this location itself.</param>
/// <param name="SubtreeCount">Animals at this location or anywhere below it.</param>
internal sealed record LocationPopulationItem(Guid LocationId, int Count, int SubtreeCount);
