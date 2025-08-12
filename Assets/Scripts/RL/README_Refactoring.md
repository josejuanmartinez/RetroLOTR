# StrategyGameAgent Refactoring

## Overview
The StrategyGameAgent has been refactored from a monolithic 730-line class into a clean, modular architecture with specialized components. This improves maintainability, testability, and clarity of the ML pipeline.

## ML Pipeline Flow
The core ML pipeline remains unchanged:
```
RequestDecision() → CollectObservations() → WriteDiscreteActionMask() → Heuristic() or model inference → OnActionReceived()
```

## New Architecture

### 1. StrategyGameAgent (Main Class)
**Responsibility**: ML Agent coordination and pipeline management
- Manages the three-phase decision process: Objectives → Movement → Action
- Coordinates between specialized components
- Handles ML-specific concerns (rewards, episodes, action masking)

**Key Methods**:
- `CollectObservations()` - Delegates to ObservationBuilder
- `OnActionReceived()` - Orchestrates the three-phase decision process
- `WriteDiscreteActionMask()` - Masks unavailable actions

### 2. ObjectiveEvaluator
**Responsibility**: Hex objective evaluation and strategic scoring
- Evaluates all hex objectives (Attack, Defense, Resource, Territory, Artifact, Safety)
- Determines the best objective type for each hex
- Provides candidate hexes sorted by objective scores

**Key Methods**:
- `EvaluateHexObjectives()` - Scores all hexes for all objective types
- `GetCandidateHexes()` - Returns sorted candidates for specific objectives

### 3. MovementPlanner
**Responsibility**: Movement strategies and pathfinding
- Plans movement towards target hexes using different strategies
- Executes movement commands
- Calculates movement rewards

**Key Methods**:
- `PlanMovement()` - Plans movement using Direct/Cautious/Aggressive strategies
- `ExecuteMovement()` - Executes the planned movement

### 4. ActionExecutor
**Responsibility**: Action execution and reward calculation
- Executes chosen actions
- Captures game state before/after actions
- Calculates and applies detailed rewards
- Handles game end conditions

**Key Methods**:
- `ExecuteAction()` - Main execution method with reward calculation
- `CaptureGameState()` - Snapshots game state for reward calculation

### 5. ObservationBuilder
**Responsibility**: ML observation construction
- Builds structured observations for the ML model
- Maintains observation space consistency
- Provides debug validation for observation counts

**Key Methods**:
- `BuildObservations()` - Constructs complete observation vector
- `BuildDummyObservations()` - Provides safe observations when episode hasn't begun

## Benefits of Refactoring

### 1. Separation of Concerns
- Each class has a single, clear responsibility
- Easier to understand and modify individual components
- Reduced coupling between different aspects of the system

### 2. Maintainability
- Changes to reward calculation don't affect observation building
- Movement logic is isolated from objective evaluation
- Easier to debug specific aspects of the agent's behavior

### 3. Testability
- Each component can be unit tested independently
- Mock dependencies can be easily injected
- Specific behaviors can be tested in isolation

### 4. Extensibility
- New objective types can be added to ObjectiveEvaluator
- New movement strategies can be added to MovementPlanner
- New reward schemes can be implemented in ActionExecutor

### 5. Code Clarity
- The main StrategyGameAgent class is now focused on ML pipeline coordination
- Complex logic is encapsulated in appropriately named classes
- The three-phase decision process is clearly visible

## Migration Notes

### Observation Space
- Observation space remains exactly the same (155 observations)
- All observation constants are preserved in ObservationBuilder
- Debug validation ensures observation count consistency

### Action Space
- Action space structure is unchanged (4 branches)
- Action masking logic is preserved
- Three-phase decision process is maintained

### Reward Structure
- All existing rewards are preserved in ActionExecutor
- Reward calculations are identical to the original implementation
- Game end conditions are handled the same way

## Usage

The refactored agent maintains the same public interface:
```csharp
// Initialize new turn
agent.NewTurn(isPlayerControlled, isTrainingMode);

// Handle player actions (if applicable)
agent.FeedbackWithPlayerActions(chosenAction);

// Access agent state
var character = agent.GetCharacter();
var availableActions = agent.GetAvailableActions();
var chosenAction = agent.GetChosenAction();
```

## File Structure
```
Assets/Scripts/RL/
├── StrategyGameAgent.cs      # Main ML Agent (refactored)
├── ObjectiveEvaluator.cs     # Hex objective evaluation
├── MovementPlanner.cs        # Movement strategies
├── ActionExecutor.cs         # Action execution & rewards
├── ObservationBuilder.cs     # ML observation construction
├── GameState.cs             # Game state helper (existing)
└── README_Refactoring.md    # This documentation
```

The refactoring maintains full backward compatibility while providing a much cleaner, more maintainable codebase for future development.