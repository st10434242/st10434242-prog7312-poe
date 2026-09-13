namespace SmartX.Core;

// One level of the hierarchy, e.g. FacilityA or Zone1. Built from the registered sensors rather
// than stored separately, so the tree cannot drift out of step with the registry.
public sealed class DeploymentNode
{
    public required string Name { get; init; }

    public List<DeploymentNode> Children { get; } = [];

    public List<SensorRegistration> Sensors { get; } = [];

    public static DeploymentNode Build(IEnumerable<SensorRegistration> registrations)
    {
        DeploymentNode root = new() { Name = "root" };

        foreach (SensorRegistration registration in registrations)
        {
            DeploymentNode current = root;

            foreach (string segment in registration.Location.Segments)
            {
                DeploymentNode? child = current.Children
                    .FirstOrDefault(c => string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));

                if (child is null)
                {
                    child = new DeploymentNode { Name = segment };
                    current.Children.Add(child);
                }

                current = child;
            }

            current.Sensors.Add(registration);
        }

        return root;
    }
}
