# v1, initial implementation
The code was generated by AI: https://chatgpt.com/share/afe5d7ba-4135-4d37-8893-c4fa878ae2e2  
GPT told me that this can be called:
- "Iterative Weighted Consensus Algorithm",
- or "Expectation-Maximization (EM) Algorithm",
- or "Probabilistic Iterative Refinement Algorithm for Tonality Detection"

Default parameters: 
- MinLoopsPerSong = 2, 
- MaxLoopsPerSong = 7, 
- TotalSongs = 1000,
- TotalLoops = 1500,
- ModulationProbability = 10%,
- BadCycleProbability = 10%

## Default parameters

Iteration 29, Max Change: 0.101709  
Iteration 30, Max Change: 0.102203  
Iteration 31, Max Change: 0.101902  
Iteration 32, Max Change: 0.009812  
Converged after 32 iterations.

### Summary of Scores:
Good Songs: Min Score = **0.3462**, Max Score = 1.0000, Average Score = **0.9406**, Median = **1.0000**, 90th Percentile = 1.0000  
Bad Songs: Min Score = **0.2319**, Max Score = 1.0000, Average Score = **0.5578**, Median = **0.5102**, 90th Percentile = 1.0000  
Good Loops: Min Score = **0.2973**, Max Score = 1.0000, Average Score = **0.9398**, Median = **1.0000**, 90th Percentile = 1.0000  
Bad Loops: Min Score = **0.2789**, Max Score = 1.0000, Average Score = **0.6868**, Median = **0.5699**, 90th Percentile = 1.0000  

### Bad Songs with Incorrectly Known Tonality:
Song song160, Known Tonality: 7, Predicted Tonality: 7, Secret Tonalities: 5, Score: 1.0000  
Song song785, Known Tonality: 9, Predicted Tonality: 9, Secret Tonalities: 2,4, Score: 0.6063  
Incorrectly Detected Songs with Incorrectly Known Tonality: 2 / 11 **(18.18%)**  

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 989 / 1000 **(98.90%)**  
Correctly Detected Loop Tonalities: 1389 / 1500 **(92.60%)**

## Min,MaxLoopsPerSong = 12, 27

Iteration 11, Max Change: 0.018081  
Iteration 12, Max Change: 0.060269  
Iteration 13, Max Change: 0.061940  
Iteration 14, Max Change: 0.056202  
Iteration 15, Max Change: 0.008873  
Converged after 15 iterations.

### Summary of Scores:
Good Songs: Min Score = **0.4330**, Max Score = 1.0000, Average Score = **0.8637**, Median = **0.8808**, 90th Percentile = 1.0000  
Bad Songs: Min Score = **0.2074**, Max Score = 0.5599, Average Score = **0.3870**, Median = **0.3808**, 90th Percentile = 0.5061  
Good Loops: Min Score = **0.3372**, Max Score = 1.0000, Average Score = **0.8260**, Median = **0.8362**, 90th Percentile = 1.0000  
Bad Loops: Min Score = **0.2173**, Max Score = 0.6574, Average Score = **0.4019**, Median = **0.3778**, 90th Percentile = 0.5498

### Bad Songs with Incorrectly Known Tonality:
Incorrectly Detected Songs with Incorrectly Known Tonality: 0 / 10 **(0.00%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonalities: 1500 / 1500 **(100.00%)**

## Min,MaxLoopsPerSong = 4, 10, TotalSongs,Loops = 1000, 400
Iteration 14, Max Change: 0.014189  
Iteration 15, Max Change: 0.070235  
Iteration 16, Max Change: 0.142608  
Iteration 17, Max Change: 0.031940  
Iteration 18, Max Change: 0.008704  
Converged after 18 iterations.

### Summary of Scores:
Good Songs: Min Score = **0.3268**, Max Score = 1.0000, Average Score = **0.8955**, Median = **1.0000**, 90th Percentile = 1.0000  
Bad Songs: Min Score = **0.2359**, Max Score = 1.0000, Average Score = **0.4570**, Median = **0.4361**, 90th Percentile = 0.5814  
Good Loops: Min Score = **0.4257**, Max Score = 1.0000, Average Score = **0.8613**, Median = **0.8972**, 90th Percentile = 1.0000  
Bad Loops: Min Score = **0.2520**, Max Score = 0.6185, Average Score = **0.3874**, Median = **0.3521**, 90th Percentile = 0.5330

### Bad Songs with Incorrectly Known Tonality:
Incorrectly Detected Songs with Incorrectly Known Tonality: 0 / 6 **(0.00%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonalities: 400 / 400 **(100.00%)**

# v2, two-dimensional probabilities and scores
Default parameters:
- SongKnownTonalityScaleMutationProbability = 18%,
- LoopLinkScaleMutationProbability = 18%
## Default parameters
Iteration 28, Max Change: 0.012664  
Iteration 29, Max Change: 0.076865  
Iteration 30, Max Change: 0.088967  
Iteration 31, Max Change: 0.023086  
Iteration 32, Max Change: 0.009383  
Converged after 32 iterations.

### Summary of Scores:
Good Songs (Tonic): Min Score = **0.2572**, Max Score = 1.0000, Average Score = **0.8876**, Median = **1.0000**, 90th Percentile = 1.0000  
Good Songs (Scale): Min Score = 0.0417, Max Score = 0.4284, Average Score = 0.1326, Median = 0.1165, 90th Percentile = 0.2040  
Bad Songs (Tonic): Min Score = **0.1739**, Max Score = 1.0000, Average Score = **0.4206**, Median = **0.3959**, 90th Percentile = 0.5797  
Bad Songs (Scale): Min Score = 0.0588, Max Score = 0.2221, Average Score = 0.0840, Median = 0.0784, 90th Percentile = 0.1050  
Good Loops (Tonic): Min Score = **0.2513**, Max Score = 1.0000, Average Score = **0.8375**, Median = **1.0000**, 90th Percentile = 1.0000  
Good Loops (Scale): Min Score = 0.0417, Max Score = 1.0000, Average Score = 0.1941, Median = 0.1169, 90th Percentile = 0.3148  
Bad Loops (Tonic): Min Score = **0.1928**, Max Score = 1.0000, Average Score = **0.6336**, Median = **0.5429**, 90th Percentile = 1.0000  
Bad Loops (Scale): Min Score = 0.0551, Max Score = 1.0000, Average Score = 0.1168, Median = 0.0997, 90th Percentile = 0.1505

### Bad Songs with Incorrectly Known Tonality:
Song song93, Known Tonality: (0, Minor), Predicted Tonality: (0, Minor), Secret Tonalities: (9, Major), Score: (0.5, 0.11801968597136918)  
Song song161, Known Tonality: (1, Major), Predicted Tonality: (1, Major), Secret Tonalities: (11, Major), Score: (0.5136164787052137, 0.15555992092659351)  
Song song387, Known Tonality: (0, Minor), Predicted Tonality: (0, Minor), Secret Tonalities: (2, Minor), Score: (1, 0.27461427719073617)  
Song song581, Known Tonality: (8, Minor), Predicted Tonality: (8, Minor), Secret Tonalities: (7, Major), Score: (1, 0.15810976282619407)  
Song song659, Known Tonality: (11, Major), Predicted Tonality: (11, Major), Secret Tonalities: (5, Minor), Score: (1, 0.2284410758039147)  
Song song716, Known Tonality: (6, Minor), Predicted Tonality: (6, Minor), Secret Tonalities: (2, Major), Score: (0.6961357760544495, 0.09194342648564809)  
Incorrectly Detected Songs with Incorrectly Known Tonality: 6 / 8 **(75.00%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 787 / 1000 **(78.70%)**  
Correctly Detected Loop Tonalities: 1216 / 1500 **(81.07%)**

### Accuracy of Detected Tonics:
Correctly Detected Song Tonics: 961 / 1000 **(96.10%)**  
Correctly Detected Loop Tonics: 1334 / 1500 **(88.93%)**

## Min,MaxLoopsPerSong = 12, 27
Iteration 10, Max Change: 0.012594  
Iteration 11, Max Change: 0.010115  
Iteration 12, Max Change: 0.008171  
Converged after 12 iterations.

### Summary of Scores:
Good Songs (Tonic): Min Score = **0.4128**, Max Score = 1.0000, Average Score = **0.8696**, Median = **0.9131**, 90th Percentile = 1.0000  
Good Songs (Scale): Min Score = 0.0720, Max Score = 0.1346, Average Score = 0.0983, Median = 0.0967, 90th Percentile = 0.1122  
Bad Songs (Tonic): Min Score = **0.2340**, Max Score = 1.0000, Average Score = **0.3712**, Median = **0.3368**, 90th Percentile = 0.4926  
Bad Songs (Scale): Min Score = 0.0579, Max Score = 0.1063, Average Score = 0.0688, Median = 0.0666, 90th Percentile = 0.0767  
Good Loops (Tonic): Min Score = **0.1976**, Max Score = 1.0000, Average Score = **0.6541**, Median = **0.6457**, 90th Percentile = 1.0000  
Good Loops (Scale): Min Score = 0.0618, Max Score = 0.1875, Average Score = 0.0989, Median = 0.0958, 90th Percentile = 0.1235  
Bad Loops (Tonic): Min Score = **0.1913**, Max Score = 1.0000, Average Score = **0.4723**, Median = **0.3862**, 90th Percentile = 0.8010  
Bad Loops (Scale): Min Score = 0.0616, Max Score = 0.1423, Average Score = 0.0857, Median = 0.0782, 90th Percentile = 0.1154

### Bad Songs with Incorrectly Known Tonality:
Song song307, Known Tonality: (6, Minor), Predicted Tonality: (3, Minor), Secret Tonalities: (3, Major), Score: (1, 0.09765070388643379)  
Incorrectly Detected Songs with Incorrectly Known Tonality: 1 / 5 **(20.00%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 815 / 1000 **(81.50%)**  
Correctly Detected Loop Tonalities: 1474 / 1500 **(98.27%)**

### Accuracy of Detected Tonics:
Correctly Detected Song Tonics: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonics: 1500 / 1500 **(100.00%)**

## Min,MaxLoopsPerSong = 4, 10, TotalSongs,Loops = 1000, 400
Iteration 8, Max Change: 0.024452  
Iteration 9, Max Change: 0.030520  
Iteration 10, Max Change: 0.014339  
Iteration 11, Max Change: 0.011191  
Iteration 12, Max Change: 0.008686  
Converged after 12 iterations.

### Summary of Scores:
Good Songs (Tonic): Min Score = **0.2880**, Max Score = 1.0000, Average Score = **0.8942**, Median = **1.0000**, 90th Percentile = 1.0000  
Good Songs (Scale): Min Score = 0.0641, Max Score = 0.1625, Average Score = 0.1026, Median = 0.0993, 90th Percentile = 0.1284  
Bad Songs (Tonic): Min Score = **0.1892**, Max Score = 0.6255, Average Score = **0.3997**, Median = **0.3770**, 90th Percentile = 0.5070  
Bad Songs (Scale): Min Score = 0.0549, Max Score = 0.0909, Average Score = 0.0704, Median = 0.0689, 90th Percentile = 0.0831  
Good Loops (Tonic): Min Score = **0.2053**, Max Score = 1.0000, Average Score = **0.6359**, Median = **0.6289**, 90th Percentile = 0.9184  
Good Loops (Scale): Min Score = 0.0606, Max Score = 0.2341, Average Score = 0.1009, Median = 0.0942, 90th Percentile = 0.1332  
Bad Loops (Tonic): Min Score = **0.1795**, Max Score = 0.7691, Average Score = **0.4170**, Median = **0.3683**, 90th Percentile = 0.6736  
Bad Loops (Scale): Min Score = 0.0598, Max Score = 0.1280, Average Score = 0.0843, Median = 0.0813, 90th Percentile = 0.1055

### Bad Songs with Incorrectly Known Tonality:
Song song86, Known Tonality: (2, Minor), Predicted Tonality: (0, Major), Secret Tonalities: (0, Minor), Score: (0.7055115786525331, 0.07973759474364309)  
Song song946, Known Tonality: (1, Major), Predicted Tonality: (11, Minor), Secret Tonalities: (11, Major), Score: (1, 0.11440961341227648)  
Incorrectly Detected Songs with Incorrectly Known Tonality: 2 / 6 **(33.33%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 807 / 1000 **(80.70%)**  
Correctly Detected Loop Tonalities: 394 / 400 **(98.50%)**

### Accuracy of Detected Tonics:
Correctly Detected Song Tonics: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonics: 400 / 400 **(100.00%)**

## Both MutationProbabilities = 0%
Iteration 20, Max Change: 0.026403  
Iteration 21, Max Change: 0.014120  
Iteration 22, Max Change: 0.081888  
Iteration 23, Max Change: 0.035534  
Iteration 24, Max Change: 0.023945  
Iteration 25, Max Change: 0.016149  
Iteration 26, Max Change: 0.010721  
Iteration 27, Max Change: 0.008268  
Converged after 27 iterations.

### Summary of Scores:
Good Songs (Tonic): Min Score = **0.2581**, Max Score = 1.0000, Average Score = **0.9101**, Median = **1.0000**, 90th Percentile = 1.0000  
Good Songs (Scale): Min Score = 0.0626, Max Score = 0.9729, Average Score = 0.1860, Median = 0.1654, 90th Percentile = 0.2835  
Bad Songs (Tonic): Min Score = **0.1751**, Max Score = 1.0000, Average Score = **0.4171**, Median = **0.3886**, 90th Percentile = 0.5460  
Bad Songs (Scale): Min Score = 0.0658, Max Score = 0.3084, Average Score = 0.1068, Median = 0.1010, 90th Percentile = 0.1355  
Good Loops (Tonic): Min Score = **0.2572**, Max Score = 1.0000, Average Score = **0.8476**, Median = **1.0000**, 90th Percentile = 1.0000  
Good Loops (Scale): Min Score = 0.0613, Max Score = 1.0000, Average Score = 0.2306, Median = 0.1615, 90th Percentile = 0.3869  
Bad Loops (Tonic): Min Score = **0.2174**, Max Score = 1.0000, Average Score = **0.6177**, Median = **0.5291**, 90th Percentile = 1.0000  
Bad Loops (Scale): Min Score = 0.0726, Max Score = 1.0000, Average Score = 0.1655, Median = 0.1267, 90th Percentile = 0.2183

### Bad Songs with Incorrectly Known Tonality:
Song song357, Known Tonality: (10, Major), Predicted Tonality: (10, Major), Secret Tonalities: (2, Major), Score: (0.5565754052726155, 0.20269245937411848)  
Song song610, Known Tonality: (11, Major), Predicted Tonality: (11, Major), Secret Tonalities: (7, Major), Score: (0.5039318656375552, 0.1471780945549856)  
Song song862, Known Tonality: (0, Major), Predicted Tonality: (0, Major), Secret Tonalities: (10, Minor),(9, Minor), Score: (0.4173206473111049, 0.12675638752324422)  
Incorrectly Detected Songs with Incorrectly Known Tonality: 3 / 7 **(42.86%)**

### Accuracy of Detected Tonalities:  
Correctly Detected Song Tonalities: 984 / 1000 **(98.40%)**  
Correctly Detected Loop Tonalities: 1361 / 1500 **(90.73%)**

### Accuracy of Detected Tonics:
Correctly Detected Song Tonics: 985 / 1000 **(98.50%)**  
Correctly Detected Loop Tonics: 1372 / 1500 **(91.47%)**


## Min,MaxLoopsPerSong = 12, 27, Both MutationProbabilities = 0%
Iteration 12, Max Change: 0.011939  
Iteration 13, Max Change: 0.056266  
Iteration 14, Max Change: 0.060556  
Iteration 15, Max Change: 0.064388  
Iteration 16, Max Change: 0.006483  
Converged after 16 iterations.

### Summary of Scores:
Good Songs (Tonic): Min Score = **0.4422**, Max Score = 1.0000, Average Score = **0.8535**, Median = **0.8730**, 90th Percentile = 1.0000  
Good Songs (Scale): Min Score = 0.1032, Max Score = 0.1992, Average Score = 0.1399, Median = 0.1394, 90th Percentile = 0.1572  
Bad Songs (Tonic): Min Score = **0.2346**, Max Score = 0.9523, Average Score = **0.3758**, Median = **0.3807**, 90th Percentile = 0.4809  
Bad Songs (Scale): Min Score = 0.0748, Max Score = 0.1162, Average Score = 0.0899, Median = 0.0872, 90th Percentile = 0.1034  
Good Loops (Tonic): Min Score = **0.2018**, Max Score = 1.0000, Average Score = **0.6987**, Median = **0.6861**, 90th Percentile = 1.0000  
Good Loops (Scale): Min Score = 0.0784, Max Score = 0.3294, Average Score = 0.1437, Median = 0.1385, 90th Percentile = 0.1811  
Bad Loops (Tonic): Min Score = **0.2023**, Max Score = 1.0000, Average Score = **0.4834**, Median = **0.4045**, 90th Percentile = 0.8179  
Bad Loops (Scale): Min Score = 0.0717, Max Score = 0.2425, Average Score = 0.1180, Median = 0.1079, 90th Percentile = 0.1628

### Bad Songs with Incorrectly Known Tonality:
Incorrectly Detected Songs with Incorrectly Known Tonality: 0 / 9 **(0.00%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonalities: 1499 / 1500 **(99.93%)**

### Accuracy of Detected Tonics:
Correctly Detected Song Tonics: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonics: 1499 / 1500 **(99.93%)**

## Min,MaxLoopsPerSong = 4, 10, TotalSongs,Loops = 1000, 400, Both MutationProbabilities = 0%
Iteration 12, Max Change: 0.013536  
Iteration 13, Max Change: 0.010681  
Iteration 14, Max Change: 0.008430  
Converged after 14 iterations.

### Summary of Scores:
Good Songs (Tonic): Min Score = **0.3548**, Max Score = 1.0000, Average Score = **0.9204**, Median = **1.0000**, 90th Percentile = 1.0000  
Good Songs (Scale): Min Score = 0.0913, Max Score = 0.2417, Average Score = 0.1560, Median = 0.1561, 90th Percentile = 0.1829  
Bad Songs (Tonic): Min Score = **0.2298**, Max Score = 0.6830, Average Score = **0.4066**, Median = **0.4048**, 90th Percentile = 0.5073  
Bad Songs (Scale): Min Score = 0.0770, Max Score = 0.1206, Average Score = 0.0964, Median = 0.0957, 90th Percentile = 0.1111  
Good Loops (Tonic): Min Score = **0.2559**, Max Score = 1.0000, Average Score = **0.6502**, Median = **0.6398**, 90th Percentile = 0.9604  
Good Loops (Scale): Min Score = 0.0896, Max Score = 0.3048, Average Score = 0.1518, Median = 0.1482, 90th Percentile = 0.1897  
Bad Loops (Tonic): Min Score = **0.2026**, Max Score = 1.0000, Average Score = **0.4860**, Median = **0.4050**, 90th Percentile = 0.7481  
Bad Loops (Scale): Min Score = 0.0819, Max Score = 0.2107, Average Score = 0.1228, Median = 0.1155, 90th Percentile = 0.1766

### Bad Songs with Incorrectly Known Tonality:
Incorrectly Detected Songs with Incorrectly Known Tonality: 0 / 10 **(0.00%)**

### Accuracy of Detected Tonalities:
Correctly Detected Song Tonalities: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonalities: 400 / 400 **(100.00%)**

### Accuracy of Detected Tonics:
Correctly Detected Song Tonics: 1000 / 1000 **(100.00%)**  
Correctly Detected Loop Tonics: 400 / 400 **(100.00%)**