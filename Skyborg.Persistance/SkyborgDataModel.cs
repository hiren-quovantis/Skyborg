namespace Skyborg.Persistance
{
    using Model;
    using System;
    using System.Data.Entity;
    using System.Linq;

    public class SkyborgDataModel : DbContext
    {
        // Your context has been configured to use a 'SkyborgDataModel' connection string from your application's 
        // configuration file (App.config or Web.config). By default, this connection string targets the 
        // 'Skyborg.Model.SkyborgDataModel' database on your LocalDb instance. 
        // 
        // If you wish to target a different database and/or database provider, modify the 'SkyborgDataModel' 
        // connection string in the application configuration file.
        public SkyborgDataModel()
            : base("name=SkyborgDataModel")
        {
            Database.SetInitializer<SkyborgDataModel>(new CreateDatabaseIfNotExists<SkyborgDataModel>());
        }

        public virtual DbSet<User> Users { get; set; }

        public DbSet<GoogleOAuthItem> GoogleOAuthItem { get; set; }

        // Add a DbSet for each entity type that you want to include in your model. For more information 
        // on configuring and using a Code First model, see http://go.microsoft.com/fwlink/?LinkId=390109.

        // public virtual DbSet<MyEntity> MyEntities { get; set; }
    }

    //public class MyEntity
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //}
}