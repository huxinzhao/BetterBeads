namespace BetterBeads.Data;

/// <summary>Only the latest request can publish; cancellation and failures are always observed.</summary>
internal sealed class LatestWork<T> : IDisposable where T:class
{
    private sealed record Outcome(T? Value,Exception? Error);
    private CancellationTokenSource? cancel;
    private Task<Outcome>? task;
    public bool Pending=>task is not null;
    public void Request(Func<CancellationToken,T> work,int delayMilliseconds=150)
    {
        Cancel();var source=new CancellationTokenSource();cancel=source;
        task=Task.Run(async()=>
        {
            try{await Task.Delay(delayMilliseconds,source.Token).ConfigureAwait(false);return new Outcome(work(source.Token),null);}
            catch(OperationCanceledException){return new Outcome(null,null);}
            catch(Exception e){return new Outcome(null,e);}
            finally{source.Dispose();}
        });
    }
    public bool TryTake(out T? value,out Exception? error)
    {
        value=null;error=null;if(task is not {IsCompleted:true} done)return false;
        task=null;cancel=null;var outcome=done.GetAwaiter().GetResult();value=outcome.Value;error=outcome.Error;return true;
    }
    public void Cancel(){try{cancel?.Cancel();}catch(ObjectDisposedException){}cancel=null;task=null;}
    public void Dispose()=>Cancel();
}
