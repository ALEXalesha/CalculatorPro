using CalcPro.Wpf.Commands;
using CalcPro.Wpf.Models;
using CalcPro.Wpf.Services;
using Xunit;

namespace CalcPro.Tests;

public class HistoryManagerTests
{
    [Fact]
    public void DigitCommand_AppliesAndUndoes()
    {
        var state = new CalcState();
        var cmd = new DigitCommand("5");
        cmd.Execute(state);
        Assert.Equal("5", state.Display);
        cmd.Undo(state);
        Assert.Equal("0", state.Display);
        Assert.Equal(CalcPhase.Idle, state.Phase);
    }

    [Fact]
    public void DigitChain_BuildsNumber()
    {
        var state = new CalcState();
        new DigitCommand("1").Execute(state);
        new DigitCommand("2").Execute(state);
        new DigitCommand("3").Execute(state);
        Assert.Equal("123", state.Display);
    }

    [Fact]
    public void UndoRedo_RestoresStateAcrossSteps()
    {
        var state = new CalcState();
        var history = new HistoryManager();
        var c1 = new DigitCommand("7");
        var c2 = new DigitCommand("8");

        c1.Execute(state);
        history.Record(c1);
        c2.Execute(state);
        history.Record(c2);

        Assert.Equal("78", state.Display);

        history.Undo(state);
        Assert.Equal("7", state.Display);

        history.Undo(state);
        Assert.Equal("0", state.Display);

        history.Redo(state);
        Assert.Equal("7", state.Display);
    }

    [Fact]
    public void NewCommandAfterUndo_ClearsRedoStack()
    {
        var state = new CalcState();
        var history = new HistoryManager();
        var c1 = new DigitCommand("1");
        var c2 = new DigitCommand("2");
        var c3 = new DigitCommand("3");

        c1.Execute(state); history.Record(c1);
        c2.Execute(state); history.Record(c2);
        history.Undo(state);
        Assert.True(history.CanRedo);

        c3.Execute(state); history.Record(c3);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void EqualsCommand_AddsHistoryEntryAndRevertsOnUndo()
    {
        // Simulate "2 + 3" mid-typing: expression has "2 + ", display has "3".
        var state = new CalcState
        {
            Expression = "2 + ",
            Display = "3",
            Phase = CalcPhase.EnteringDigit
        };
        var hm = new HistoryManager();
        var eq = new EqualsCommand(hm);
        eq.Execute(state);
        Assert.Single(hm.Entries);
        Assert.Equal("5", state.Display);

        eq.Undo(state);
        Assert.Empty(hm.Entries);
        Assert.Equal("3", state.Display);
    }
}
