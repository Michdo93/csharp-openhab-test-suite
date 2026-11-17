using System.Collections.Generic;
using System.Linq;

namespace OpenHABTestSuite
{
    /// <summary>
    /// The state a test must reach: one state, several accepted states, or null (then the test
    /// only checks that openHAB accepts the command).
    /// <code>
    /// tester.TestSwitch("Licht", "ON", "ON");
    /// tester.TestDimmer("Dimmer", "30", new[] { "30", "30.0" }, 5);
    /// tester.TestSwitch("Licht", "OFF");
    /// </code>
    /// </summary>
    public sealed class ExpectedState
    {
        readonly string[] _states;
        readonly bool _list;

        /// <summary>Exactly one accepted state.</summary>
        public ExpectedState(string state) { _states = new[] { state }; }
        /// <summary>Several accepted states.</summary>
        public ExpectedState(IEnumerable<string> states) { _states = states.ToArray(); _list = true; }

        /// <summary>The accepted states.</summary>
        public IReadOnlyList<string> States => _states;

        /// <summary>True if <paramref name="state"/> is one of the accepted states.</summary>
        public bool Matches(string state) => _states.Contains(state);

        /// <summary>'ON' or ['30', '30.0'] – as Python prints it.</summary>
        public override string ToString() =>
            _list ? "[" + string.Join(", ", _states.Select(s => "'" + s + "'")) + "]" : "'" + _states[0] + "'";

        internal bool IsList => _list;

        /// <summary>"ON" becomes an ExpectedState.</summary>
        public static implicit operator ExpectedState?(string? state) => state == null ? null : new ExpectedState(state);
        /// <summary>new[] { "30", "30.0" } becomes an ExpectedState.</summary>
        public static implicit operator ExpectedState?(string[]? states) => states == null ? null : new ExpectedState(states);
        /// <summary>A list becomes an ExpectedState.</summary>
        public static implicit operator ExpectedState?(List<string>? states) => states == null ? null : new ExpectedState(states);
    }
}
