using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyborg.Model
{
    public class GoogleOAuthItem
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; }

        [MaxLength(50)]
        public string ConversationId { get; set; }

        [MaxLength(500)]
        public string Value { get; set; }
    }
}
