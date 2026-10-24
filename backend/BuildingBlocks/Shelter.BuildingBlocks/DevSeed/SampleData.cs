using System.Globalization;

namespace Shelter.BuildingBlocks.DevSeed;

/// <summary>
/// Fictional Québec sample values for the development seed: names, addresses, phones, pet names and short notes.
/// Phone numbers use the 555-01xx range reserved for fiction, and emails use <c>example.com</c>/<c>example.org</c>.
/// French first, with some English, like the pilot regions.
/// </summary>
public static class SampleData
{
    public static IReadOnlyList<string> FrenchFirstNames { get; } =
    [
        "Marie", "Julie", "Catherine", "Isabelle", "Geneviève", "Mélanie", "Valérie", "Émilie", "Camille", "Léa",
        "Chloé", "Rosalie", "Florence", "Maude", "Audrey", "Josée", "Nathalie", "Sophie", "Annie", "Marie-Ève",
        "Jean", "Martin", "François", "Mathieu", "Simon", "Maxime", "Alexandre", "Olivier", "Gabriel", "Samuel",
        "Félix", "Louis", "Étienne", "Jérôme", "Sébastien", "Patrick", "Éric", "Benoît", "Luc", "Jean-François",
    ];

    public static IReadOnlyList<string> EnglishFirstNames { get; } =
    [
        "Emily", "Sarah", "Jessica", "Hannah", "Olivia", "Megan", "Rachel", "Laura", "Grace", "Kate",
        "James", "Michael", "David", "Ryan", "Daniel", "Andrew", "Thomas", "Matthew", "Kevin", "Liam",
    ];

    public static IReadOnlyList<string> FrenchLastNames { get; } =
    [
        "Tremblay", "Gagnon", "Roy", "Côté", "Bouchard", "Gauthier", "Morin", "Lavoie", "Fortin", "Gagné",
        "Ouellet", "Pelletier", "Bélanger", "Lévesque", "Bergeron", "Leblanc", "Paquette", "Girard", "Simard", "Boucher",
        "Caron", "Beaulieu", "Cloutier", "Dubé", "Poirier", "Fournier", "Lapointe", "Leclerc", "Lefebvre", "Poulin",
        "Thibault", "St-Pierre", "Nadeau", "Martin", "Landry", "Martel", "Bédard", "Grenier", "Lessard", "Bernier",
    ];

    public static IReadOnlyList<string> EnglishLastNames { get; } =
    [
        "Smith", "Brown", "Wilson", "MacDonald", "Campbell", "Taylor", "Anderson", "Murphy", "Johnston", "Stewart",
        "Robertson", "Kelly", "Reid", "Scott", "Mitchell",
    ];

    /// <summary>Organizations a shelter deals with (partner rescues, clinics, donors).</summary>
    public static IReadOnlyList<string> OrganizationNames { get; } =
    [
        "Clinique vétérinaire des Laurentides", "Refuge Pattes et Museaux", "Animalerie Le Bon Voisin",
        "Fondation Chats Errants Rive-Sud", "Sauvetage Canin de l'Estrie", "Hôpital vétérinaire du Plateau",
        "Ferme Les Quatre Saisons", "Coop Nourriture Animale Québec", "Rescue Pointe-Claire", "Pet Rescue Outaouais",
    ];

    /// <summary>Street name, city, postal prefix (forward sortation area) and area code.</summary>
    public static IReadOnlyList<(string Street, string City, string PostalPrefix, string AreaCode)> Places { get; } =
    [
        ("rue Saint-Denis", "Montréal", "H2J", "514"),
        ("avenue du Mont-Royal Est", "Montréal", "H2H", "514"),
        ("rue Ontario Est", "Montréal", "H1W", "514"),
        ("boulevard Saint-Laurent", "Montréal", "H2T", "514"),
        ("rue Wellington", "Montréal", "H4G", "514"),
        ("rue Notre-Dame Ouest", "Montréal", "H4C", "514"),
        ("boulevard des Laurentides", "Laval", "H7G", "450"),
        ("rue Saint-Charles", "Longueuil", "J4H", "450"),
        ("chemin de Chambly", "Longueuil", "J4J", "450"),
        ("rue Saint-Jean", "Québec", "G1R", "418"),
        ("boulevard Charest Est", "Québec", "G1K", "418"),
        ("avenue Cartier", "Québec", "G1R", "418"),
        ("rue King Ouest", "Sherbrooke", "J1H", "819"),
        ("rue Wellington Nord", "Sherbrooke", "J1H", "819"),
        ("boulevard des Forges", "Trois-Rivières", "G8Z", "819"),
        ("rue Principale", "Gatineau", "J9H", "819"),
        ("boulevard Talbot", "Saguenay", "G7H", "418"),
        ("rue Racine Est", "Saguenay", "G7H", "418"),
        ("rue Sainte-Catherine Ouest", "Montréal", "H3B", "514"),
        ("Sherbrooke Street West", "Montréal", "H4A", "514"),
    ];

    public static IReadOnlyList<string> DogNames { get; } =
    [
        "Max", "Charlie", "Bella", "Luna", "Rocky", "Coco", "Milo", "Daisy", "Bailey", "Ruby", "Toby", "Zoé",
        "Oscar", "Lola", "Buddy", "Maggie", "Pistache", "Caramel", "Nala", "Duke", "Moka", "Gaston", "Biscuit", "Rosie",
    ];

    public static IReadOnlyList<string> CatNames { get; } =
    [
        "Minou", "Félix", "Mimi", "Tigrou", "Simba", "Cléo", "Pacha", "Grisou", "Noisette", "Chaussette", "Olive",
        "Mochi", "Loki", "Pixel", "Praline", "Filou", "Réglisse", "Nougat", "Tofu", "Belle", "Garfield", "Kiwi",
    ];

    public static IReadOnlyList<string> SmallAnimalNames { get; } =
    [
        "Pompon", "Noisette", "Flocon", "Caramel", "Panache", "Bouboule", "Cannelle", "Pépite", "Plume", "Coton",
    ];

    public static IReadOnlyList<string> DogBreeds { get; } =
    [
        "Labrador", "Berger allemand", "Husky sibérien", "Beagle", "Pitbull", "Chihuahua", "Golden retriever",
        "Border collie", "Boxer", "Shih tzu", "Caniche", "Bouvier bernois", "Jack Russell", "Croisé",
    ];

    public static IReadOnlyList<string> CatBreeds { get; } =
    [
        "Domestique à poil court", "Domestique à poil long", "Siamois", "Maine coon", "Persan", "Bengal", "Croisé",
    ];

    public static IReadOnlyList<string> Colours { get; } =
    [
        "Noir", "Blanc", "Gris", "Roux", "Brun", "Crème", "Tigré", "Écaille de tortue", "Calico", "Noir et blanc",
        "Fauve", "Bringé", "Tricolore", "Chocolat", "Bleu",
    ];

    public static IReadOnlyList<string> Marks { get; } =
    [
        "Tache blanche sur la poitrine", "Oreille gauche entaillée", "Queue courte", "Cicatrice sur le museau",
        "Tatouage à l'oreille droite", "Yeux vairons", "Patte arrière boiteuse", "Bout de la queue blanc",
    ];

    public static IReadOnlyList<string> BehaviourAlerts { get; } =
    [
        "Craintif avec les hommes", "Ne pas mettre avec d'autres chats", "Réactif en laisse", "Protège sa nourriture",
        "Mord quand on le soulève", "Anxiété de séparation",
    ];

    public static IReadOnlyList<string> MedicalAlerts { get; } =
    [
        "Diabétique : insuline matin et soir", "Allergie au poulet", "Souffle au cœur", "Épilepsie sous traitement",
        "Insuffisance rénale : diète spéciale", "FIV positif",
    ];

    public static IReadOnlyList<string> LegalAlerts { get; } =
    [
        "Saisie : ne pas remettre au propriétaire", "Déclaré potentiellement dangereux", "Dossier en cour",
    ];

    /// <summary>Short movement notes. Fiction, with no real person in them.</summary>
    public static IReadOnlyList<string> MovementNotes { get; } =
    [
        "Trouvé près du parc", "Arrivé sans médaille", "Amené par un voisin", "Très amaigri à l'arrivée",
        "Transféré par un refuge partenaire", "Retour après une période d'essai", "Déplacé pour faire de la place",
        "Isolement préventif", "Rendez-vous de stérilisation", "Adoption en famille avec enfants",
        "Famille d'accueil temporaire", "Le propriétaire a présenté une preuve de propriété",
    ];

    public static IReadOnlyList<string> PersonNotes { get; } =
    [
        "Préfère être joint en soirée", "Disponible les fins de semaine", "A déjà adopté chez nous",
        "Bénévole au promenage des chiens", "Famille d'accueil pour chatons", "Don mensuel", "Allergique aux chats",
    ];

    /// <summary>A valid-format Canadian postal code starting with <paramref name="prefix"/>, such as <c>H2J 3K5</c>.</summary>
    public static string PostalCode(Random random, string prefix)
    {
        ArgumentNullException.ThrowIfNull(random);
        const string letters = "ABCEGHJKLMNPRSTVWXYZ";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix} {random.Next(10)}{letters[random.Next(letters.Length)]}{random.Next(10)}");
    }

    /// <summary>A fictional number such as <c>514-555-0123</c>.</summary>
    public static string Phone(Random random, string areaCode)
    {
        ArgumentNullException.ThrowIfNull(random);
        return string.Create(CultureInfo.InvariantCulture, $"{areaCode}-555-01{random.Next(100):D2}");
    }

    /// <summary>A civic number and street, such as <c>1234 rue Saint-Denis</c>.</summary>
    public static string AddressLine(Random random, string street)
    {
        ArgumentNullException.ThrowIfNull(random);
        return string.Create(CultureInfo.InvariantCulture, $"{random.Next(10, 9999)} {street}");
    }

    /// <summary>
    /// A 15-digit microchip number derived from the animal's per-organization number, so it never repeats in the
    /// organization, even across runs.
    /// </summary>
    public static string Microchip(int animalNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"124{animalNumber:D12}");
}
