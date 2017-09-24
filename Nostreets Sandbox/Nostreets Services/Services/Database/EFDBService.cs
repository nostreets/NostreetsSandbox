﻿using NostreetsORM.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nostreets_Services.Services.Database
{
    public class EFDBService<T> : IDBService<T> where T : class
    {

        public EFDBService()
        {
        }

        public EFDBService(string connectionKey)
        {
            _connectionKey = connectionKey;
        }

        private string _connectionKey = "DefaultConnection";
        private EFDBContext<T> _context = null;

        public List<T> GetAll()
        {
            List<T> result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.ToList();
            }
            return result;
        }

        public T Get(object id)
        {
            T result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.FirstOrDefault(a => a.GetType().GetProperties().GetValue(0) == id);
            }
            return result;
        }

        public object Insert(T model)
        {
            object result = null;

            var firstProp = model.GetType().GetProperties()[0];

            if (firstProp.PropertyType.Name.Contains("Int"))
            {
                model.GetType().GetProperty(firstProp.Name).SetValue(model, GetAll().Count + 1);
            }
            else if (firstProp.PropertyType.Name == "GUID")
            {
                model.GetType().GetProperties().SetValue(Guid.NewGuid().ToString(), 0);
            }

            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                _context.Table.Add(model);

                if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }

                result = model.GetType().GetProperties().GetValue(0);
            }

            return result;
        }

        public void Delete(object id)
        {
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                T obj = _context.Table.FirstOrDefault(a => a.GetType().GetProperties().GetValue(0) == id);

                _context.Table.Remove(obj);

                if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }
            }
        }

        public void Update(T model)
        {
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                T targetedUser = _context.Table.FirstOrDefault(a => a.GetType().GetProperties().GetValue(0) == model.GetType().GetProperties().GetValue(0));
                targetedUser = model;

                if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }
            }
        }

        public List<T> Where(Func<T, bool> predicate)
        {
            List<T> result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.Where(predicate).ToList();
            }
            return result;
        }

        public List<T> Where(Func<T, int, bool> predicate)
        {
            List<T> result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.Where(predicate).ToList();
            }
            return result;
        }




    }

    public class EFDBService<T, IdType> : IDBService<T, IdType> where T : class
    {

        public EFDBService()
        {
        }

        public EFDBService(string connectionKey)
        {
            _connectionKey = connectionKey;
        }

        private string _connectionKey = "DefaultConnection";
        private EFDBContext<T> _context = null;

        public List<T> GetAll()
        {
            List<T> result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.ToList();
            }
            return result;
        }

        public T Get(IdType id)
        {
            T result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.FirstOrDefault(a => a.GetType().GetProperties().GetValue(0) == (object)id);
            }
            return result;
        }

        public IdType Insert(T model)
        {
            IdType result = default(IdType);
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                var firstPTypeName = model.GetType().GetProperties()[0].GetType().Name;

                if (firstPTypeName.Contains("INT"))
                {
                    model.GetType().GetProperties().SetValue(GetAll().Count + 1, 0);
                }
                else if (firstPTypeName == "GUID")
                {
                    model.GetType().GetProperties().SetValue(Guid.NewGuid().ToString(), 0);
                }
                _context.Table.Add(model);

                if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }

                result = (IdType)model.GetType().GetProperties().GetValue(0);
            }

            return result;
        }

        public void Delete(IdType id)
        {
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                T obj = _context.Table.FirstOrDefault(a => a.GetType().GetProperties().GetValue(0) == (object)id);

                _context.Table.Remove(obj);

                if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }
            }
        }

        public void Update(T model)
        {
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                T targetedUser = _context.Table.FirstOrDefault(a => a.GetType().GetProperties().GetValue(0) == model.GetType().GetProperties().GetValue(0));
                targetedUser = model;

                if (_context.SaveChanges() == 0) { throw new Exception("DB changes not saved!"); }
            }
        }

        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            IEnumerable<T> result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.Where(predicate);
            }
            return result;
        }

        public IEnumerable<T> Where(Func<T, int, bool> predicate)
        {
            IEnumerable<T> result = null;
            using (_context = new EFDBContext<T>(_connectionKey, typeof(T).Name))
            {
                result = _context.Table.Where(predicate);
            }
            return result;
        }

    }

    class EFDBContext<TContext> : DbContext where TContext : class
    {
        public EFDBContext()
            : base("DefaultConnection")
        { }

        public EFDBContext(string connectionKey)
            : base(connectionKey)
        { }

        public EFDBContext(string connectionKey, string tableName)
            : base(connectionKey)
        {
            OnModelCreating(new DbModelBuilder().HasDefaultSchema(tableName));
        }

        public IDbSet<TContext> Table { get; set; }

    }
}
