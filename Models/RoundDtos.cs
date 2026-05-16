using System;
using System.Collections.Generic;

namespace ProjectTallify.Models
{
    // Matches the structure from collectRoundsForPayload() in create-event.js or similar
    public class SimpleRound
    {
        public string? RoundName { get; set; } // from criteria wizard
    }
}
