using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    /// <summary>
    /// Name-value pairs for admin related settings
    /// </summary>
    public class AdminData
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
