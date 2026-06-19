using System;
using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
}
