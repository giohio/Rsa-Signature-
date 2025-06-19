using MongoDB.Driver;
using RsaSignApi.Model;

namespace RsaSignApi.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext()
        {
            var client = new MongoClient("mongodb+srv://nguyenhuukien101217:nguyen123@cluster0.ywhasr7.mongodb.net/");
            _database = client.GetDatabase("Rsa_sign");
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("User");
        public IMongoCollection<Sign> Signs => _database.GetCollection<Sign>("Sign");
    }
}