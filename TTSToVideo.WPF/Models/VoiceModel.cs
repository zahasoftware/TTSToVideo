using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.Models
{
    public class VoiceModel
    {
        public string Display => $"{Name,-10} - {Gender,-10}  ({Tags?.Replace("premade", "")}) {ModelId ?? ""}";
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? ModelId { get; set; }
        public string? Gender { get; set; }
        public string? Language { get; set; }
        public string? Tags { get; set; }
    }
}
