using Skyborg.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyborg.Persistance
{
    public class UserRepository : RepositoryBase
    {
        public bool Upsert(User user)
        {
            bool isUpsert = false;
            try
            {
                using (var context = new SkyborgDataModel())
                {
                    var dbUser = context.Users.FirstOrDefault(m => m.EmailId == user.EmailId || m.Name == user.Name);

                    if (dbUser != null)
                    {
                        dbUser.GoogleRefreshToken = user.GoogleRefreshToken;

                        context.Entry(dbUser).State = EntityState.Modified;
                    }
                    else
                    {
                        context.Users.Add(user);
                    }
                    isUpsert = context.SaveChanges() > 0;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return isUpsert;
        }

        public User GetUser(string userId, string userName)
        {
            User user = null;
            try
            {
                using (var context = new SkyborgDataModel())
                {
                    user = context.Users.FirstOrDefault(m => m.EmailId == userName && m.Name == userId);
                }
            }
            catch (Exception e)
                {
                throw;
            }
            return user;
        }
    }
}
