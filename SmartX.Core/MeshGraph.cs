namespace SmartX.Core;

// Undirected weighted graph of the mesh, vertices are nodes and each one keeps a map of its links
public sealed class MeshGraph
{
    // Inner dictionary is keyed by the neighbour so finding or replacing one link is O(1)
    private readonly Dictionary<string, Dictionary<string, MeshLink>> _adjacency =
        new(StringComparer.OrdinalIgnoreCase);

    public int VertexCount => _adjacency.Count;

    // Every link is stored at both ends, so halve it
    public int EdgeCount => _adjacency.Values.Sum(links => links.Count) / 2;

    public IEnumerable<string> Vertices => _adjacency.Keys;

    public bool HasVertex(string nodeId) => _adjacency.ContainsKey(nodeId);

    public void AddVertex(string nodeId)
    {
        if (!_adjacency.ContainsKey(nodeId))
        {
            _adjacency[nodeId] = new Dictionary<string, MeshLink>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void RemoveVertex(string nodeId)
    {
        if (!_adjacency.Remove(nodeId, out Dictionary<string, MeshLink>? links))
        {
            return;
        }

        // Take the link out of the other end as well or it would point at nothing
        foreach (string neighbour in links.Keys)
        {
            _adjacency[neighbour].Remove(nodeId);
        }
    }

    // Same link object goes in both ends, a newer report just replaces it
    public void AddOrUpdateLink(MeshLink link)
    {
        if (string.Equals(link.From, link.To, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddVertex(link.From);
        AddVertex(link.To);

        _adjacency[link.From][link.To] = link;
        _adjacency[link.To][link.From] = link;
    }

    public bool RemoveLink(string a, string b)
    {
        bool removed = _adjacency.TryGetValue(a, out Dictionary<string, MeshLink>? fromA) && fromA.Remove(b);

        if (_adjacency.TryGetValue(b, out Dictionary<string, MeshLink>? fromB))
        {
            fromB.Remove(a);
        }

        return removed;
    }

    public IEnumerable<MeshLink> Neighbours(string nodeId) =>
        _adjacency.TryGetValue(nodeId, out Dictionary<string, MeshLink>? links) ? links.Values : [];

    public bool TryGetLink(string a, string b, out MeshLink? link)
    {
        link = null;
        return _adjacency.TryGetValue(a, out Dictionary<string, MeshLink>? links) && links.TryGetValue(b, out link);
    }

    // Each link once, by only taking it from the end whose id sorts first
    public IEnumerable<MeshLink> Links()
    {
        foreach ((string nodeId, Dictionary<string, MeshLink> links) in _adjacency)
        {
            foreach ((string neighbour, MeshLink link) in links)
            {
                if (string.Compare(nodeId, neighbour, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    yield return link;
                }
            }
        }
    }

    // Algorithms get their own copy so they never run while a report is changing the graph
    public MeshGraph Clone()
    {
        MeshGraph copy = new();

        foreach (string nodeId in _adjacency.Keys)
        {
            copy.AddVertex(nodeId);
        }

        foreach (MeshLink link in Links())
        {
            copy.AddOrUpdateLink(link);
        }

        return copy;
    }
}
