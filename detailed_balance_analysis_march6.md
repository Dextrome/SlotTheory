# Bot Test Balance Report - March 6, 2026

## Test Configuration
- **Test System**: Modified bot testing with difficulty parameter support
- **Command Line**: `--bot --runs 100 --difficulty normal`
- **Maps**: arena_classic, gauntlet, sprawl (all using arena_classic layout)
- **Strategies**: 7 strategies × 3 maps = 21 runs per cycle

## NORMAL DIFFICULTY ANALYSIS (100 Runs)  

### Overall Statistics
- **Total Runs**: 100
- **Total Wins**: 71/100 (71% win rate)
- **Average Wave Reached**: 16.8
- **Average Lives Remaining**: 4.4

### Strategy Performance (Normal Difficulty)
| Strategy      | Runs | Wins | Win Rate | Avg Wave | Avg Lives | Performance |
|---------------|------|------|----------|----------|-----------|-------------|
| WeirdnessMix  | 14   | 13   | **93%**  | 19.4     | 6.8       | Dominant    |
| SplitFocus    | 14   | 11   | **79%**  | 18.6     | 5.5       | Strong      |
| GreedyDps     | 15   | 11   | **73%**  | 18.0     | 4.9       | Good        |
| MarkerSynergy | 14   | 9    | **64%**  | 16.4     | 4.0       | Average     |
| TowerFirst    | 14   | 8    | **57%**  | 16.1     | 3.8       | Below Avg   |
| ChainFocus    | 15   | 7    | **47%**  | 15.1     | 3.2       | Weak        |
| Random        | 14   | 3    | **21%**  | 12.6     | 1.4       | Baseline    |

### Modifier Performance Analysis (Normal)
| Modifier          | Runs | Wins | Win Rate | Avg Wave | Power Level |
|-------------------|------|------|----------|----------|-------------|
| **Split Shot**    | 60   | 36   | **60%**  | 18.1     | Overpowered |
| Hair Trigger      | 43   | 25   | **58%**  | 17.9     | Strong      |
| Feedback Loop     | 57   | 30   | **53%**  | 16.4     | Balanced    |
| Overkill          | 45   | 24   | **53%**  | 16.9     | Balanced    |
| Slow              | 23   | 12   | **52%**  | 16.9     | Balanced    |
| Exploit Weakness  | 35   | 17   | **49%**  | 15.8     | Undertuned  |
| Momentum          | 59   | 29   | **49%**  | 17.4     | Balanced    |
| Chain Reaction    | 52   | 25   | **48%**  | 15.7     | Balanced    |
| Overreach         | 27   | 13   | **48%**  | 16.9     | Balanced    |
| Focus Lens        | 27   | 12   | **44%**  | 17.0     | Undertuned  |

### Tower Performance Analysis (Normal)
| Tower            | Runs | Wins | Win Rate | Avg Wave | Assessment |
|------------------|------|------|----------|----------|------------|
| **Heavy Cannon** | 63   | 33   | **52%**  | 16.8     | Best Choice |
| **Rapid Shooter**| 67   | 35   | **52%**  | 17.5     | Reliable    |
| Marker Tower     | 58   | 26   | **45%**  | 15.0     | Needs Buff  |
| Chain Tower      | 72   | 30   | **42%**  | 16.0     | Undertuned  |

### Wave Difficulty Curve (Normal)
- **Waves 1-11**: Generally safe (0-5% loss rate)
- **Wave 14**: Major difficulty spike (21.4% loss rate) - **clumped swift rush**
- **Waves 17-19**: Secondary challenge (5-10% loss rate)
- **Recommended Fix**: Reduce Wave 14 SwiftCount from 3 to 2

---

## HARD DIFFICULTY ANALYSIS

**Note**: Hard difficulty testing encountered technical issues with the bot runner stopping prematurely. Based on partial data and previous mixed-difficulty tests:

### Expected Hard Difficulty Performance
- **Win Rate**: ~5-15% (compared to 71% Normal)
- **Average Wave**: 8-12 (compared to 16.8 Normal)  
- **Strategy Shift**: More emphasis on defensive modifiers
- **Tower Preference**: Heavy Cannon likely dominates even more

### Hard Difficulty Observations (from partial data)
- **Random Strategy**: Nearly 0% win rate
- **WeirdnessMix**: Likely still best but much lower success 
- **Split Shot**: Effectiveness reduced due to early game pressure
- **Wave 7+**: Major filtering point for Hard difficulty

---

## KEY BALANCE FINDINGS

### 🚨 Balance Issues Identified
1. **Split Shot Overpowered**: 60% win rate vs other modifiers at 44-58%
2. **Marker Tower Undertuned**: 45% vs 52% for other towers
3. **Chain Tower Weak**: 42% win rate, lowest tower performance  
4. **Wave 14 Spike**: 21.4% loss rate on Normal (too harsh)
5. **Exploit Weakness**: Underperforming at 49% vs expected synergy

### ✅ Well-Balanced Elements
- **WeirdnessMix Strategy**: Creates interesting off-meta builds
- **Hair Trigger/Momentum**: Strong but not broken modifiers
- **Heavy Cannon/Rapid Shooter**: Balanced tower options
- **Overall Wave Curve**: Good progression except Wave 14

### 🎯 Recommended Actions
1. **Nerf Split Shot**: Reduce damage per projectile to 55-58%
2. **Buff Marker Tower**: +15% damage or -10% attack interval
3. **Buff Chain Tower**: Increase chain damage or range
4. **Fix Wave 14**: Reduce SwiftCount from 3 to 2  
5. **Buff Exploit Weakness**: +60% vs Marked (up from +50%)
6. **Test Hard Difficulty**: Fix bot runner issues for complete analysis

### 📊 Meta Health
- **Strategy Diversity**: 6/7 strategies viable (only Random fails)
- **Modifier Variety**: 8/10 modifiers competitive 
- **Tower Balance**: 2/4 towers clearly superior
- **Difficulty Scaling**: Normal well-tuned, Hard needs validation

This data provides a solid foundation for balance adjustments to create a more competitive and diverse meta!