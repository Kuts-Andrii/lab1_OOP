namespace MyExcel.Core;

internal static class CycleDetector
{
    public static Dictionary<CellAddress, IReadOnlyList<CellAddress>> FindCycles(
        IReadOnlyDictionary<CellAddress, List<CellAddress>> graph)
    {
        var result = new Dictionary<CellAddress, IReadOnlyList<CellAddress>>();
        var indices = new Dictionary<CellAddress, int>();
        var lowLinks = new Dictionary<CellAddress, int>();
        var stack = new Stack<CellAddress>();
        var onStack = new HashSet<CellAddress>();
        var nextIndex = 0;

        void StrongConnect(CellAddress vertex)
        {
            indices[vertex] = nextIndex;
            lowLinks[vertex] = nextIndex;
            nextIndex++;
            stack.Push(vertex);
            onStack.Add(vertex);

            foreach (var neighbour in graph[vertex])
            {
                if (!indices.ContainsKey(neighbour))
                {
                    StrongConnect(neighbour);
                    lowLinks[vertex] = Math.Min(lowLinks[vertex], lowLinks[neighbour]);
                }
                else if (onStack.Contains(neighbour))
                {
                    lowLinks[vertex] = Math.Min(lowLinks[vertex], indices[neighbour]);
                }
            }

            if (lowLinks[vertex] != indices[vertex])
            {
                return;
            }

            var component = new List<CellAddress>();
            CellAddress member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != vertex);

            var isCycle = component.Count > 1 || graph[vertex].Contains(vertex);
            if (!isCycle)
            {
                return;
            }

            var sorted = component.Order().ToList();
            foreach (var address in sorted)
            {
                result[address] = sorted;
            }
        }

        foreach (var vertex in graph.Keys)
        {
            if (!indices.ContainsKey(vertex))
            {
                StrongConnect(vertex);
            }
        }

        return result;
    }
}
