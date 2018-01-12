﻿using Nostreets_Services.Domain;
using NostreetsExtensions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nostreets_Services.Interfaces.Services
{
    public interface IUserService : IDBService<User, string>
    {
        bool CheckIfUserExist(string username);
        User GetByUsername(string username);
    }
}
