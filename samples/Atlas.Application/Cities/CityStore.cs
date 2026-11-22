using System.Collections.Concurrent;

namespace Atlas.Application.Cities;

/// <summary>
/// The sample's in-memory data, standing in for a database.
/// </summary>
public sealed class CityStore
{
    private readonly ConcurrentDictionary<int, CityDto> _cities = new()
    {
        [1] = new CityDto(1, "Kyiv", "UA", 2_952_301),
    };

    private readonly ConcurrentQueue<string> _audit = new();
    private int _lastId = 1;

    public IReadOnlyCollection<string> AuditLog => _audit;

    public CityDto? Find(int id) => _cities.GetValueOrDefault(id);

    public bool NameTaken(string name, string countryCode) =>
        _cities.Values.Any(city => city.CountryCode == countryCode
                                   && string.Equals(city.Name, name, StringComparison.OrdinalIgnoreCase));

    public CityDto Add(string name, string countryCode, int population)
    {
        CityDto city = new(Interlocked.Increment(ref _lastId), name, countryCode, population);
        _cities[city.Id] = city;
        return city;
    }

    public void Audit(string entry) => _audit.Enqueue(entry);
}
