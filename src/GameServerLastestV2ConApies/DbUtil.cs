using System.Collections.Concurrent;
using System.Threading;
using Org.BouncyCastle.Security;
using ProudNetSrc;

namespace Santana
{
  using System;
  using System.Collections.Generic;
  using System.Data;
  using System.Threading.Tasks;
  using Dapper.FastCrud;
  using Dapper.FastCrud.Configuration.StatementOptions.Builders;
  using Serilog;
  using Serilog.Core;

  class DbUtil
  {
    private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(DbUtil));
    public static readonly MaxUseLock Sync = new MaxUseLock(200);

    public static IDbTransaction BeginTransaction(IDbConnection db)
    {
      try
      {
        return db.BeginTransaction();
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return null;
    }

    public static TEntity Get<TEntity>(IDbConnection db, TEntity entityKeys,
        Action<ISelectSqlSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return db.Get(entityKeys, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return entityKeys;
    }

    public static async Task<TEntity> GetAsync<TEntity>(IDbConnection db, TEntity entityKeys,
        Action<ISelectSqlSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return await db.GetAsync(entityKeys, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return entityKeys;
    }

    public static int BulkDelete<TEntity>(IDbConnection db,
        Action<IConditionalBulkSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return db.BulkDelete(statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return -1;
    }

    public static async Task<int> BulkUpdateAsync<TEntity>(IDbConnection db, TEntity updateData,
        Action<IConditionalBulkSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return await db.BulkUpdateAsync(updateData, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return -1;
    }

    public static int BulkUpdate<TEntity>(IDbConnection db, TEntity updateData,
        Action<IConditionalBulkSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return db.BulkUpdate(updateData, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return -1;
    }

    public static async Task<int> BulkDeleteAsync<TEntity>(IDbConnection db,
        Action<IConditionalBulkSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return await db.BulkDeleteAsync(statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return -1;
    }

    public static IEnumerable<TEntity> Find<TEntity>(IDbConnection db,
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return db.Find(statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return new List<TEntity>();
    }

    public static async Task<IEnumerable<TEntity>> FindAsync<TEntity>(IDbConnection db,
        Action<IRangedBatchSelectSqlSqlStatementOptionsOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return await db.FindAsync(statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return new List<TEntity>();
    }

    public static bool Update<TEntity>(IDbConnection db, TEntity entityToUpdate,
        Action<IStandardSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return db.Update(entityToUpdate, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return false;
    }

    public static async Task<bool> UpdateAsync<TEntity>(IDbConnection db, TEntity entityToUpdate,
        Action<IStandardSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return await db.UpdateAsync(entityToUpdate, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return false;
    }

    public static bool Delete<TEntity>(IDbConnection db, TEntity entityToDelete,
        Action<IStandardSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return db.Delete(entityToDelete, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return false;
    }

    public static async Task<bool> DeleteAsync<TEntity>(IDbConnection db, TEntity entityToDelete,
        Action<IStandardSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        return await db.DeleteAsync(entityToDelete, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"Storage layer raised while running a statement: {dbError}");
      }

      return false;
    }

    public static void Insert<TEntity>(IDbConnection db, TEntity entityToInsert,
        Action<IStandardSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        db.Insert(entityToInsert, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"[DbUtil.Insert] Row of type {typeof(TEntity).Name} could not be persisted: {dbError}");
        throw;
      }
    }

    public static async Task InsertAsync<TEntity>(IDbConnection db, TEntity entityToInsert,
        Action<IStandardSqlStatementOptionsBuilder<TEntity>> statementOptions = null)
    {
      try
      {
        await db.InsertAsync(entityToInsert, statementOptions);
      }
      catch (Exception dbError)
      {
        Logger.Error($"[DbUtil.InsertAsync] Row of type {typeof(TEntity).Name} could not be persisted: {dbError}");
        throw;
      }
    }
  }
}
