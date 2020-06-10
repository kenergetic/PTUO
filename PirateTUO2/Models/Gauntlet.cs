using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PirateTUO2.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    public class MongoBsonDoc
    {
        [BsonIgnoreIfDefault]
        public ObjectId _id { get; set; }
        public string Name { get; set; }
        public string UploadDate { get; set; }
        public string Content { get; set; }

        public MongoBsonDoc()
        {
        }


    }
}
