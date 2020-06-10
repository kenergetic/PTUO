using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    /// <summary>
    /// Name value collection of tasks in mongo
    /// </summary>
    public class AdminTask
    {
        [BsonIgnoreIfDefault]
        public ObjectId _id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
