# fantasy-f1


Console app for suggesting F1 Grid lineup.

It uses the f1 open data to fetch the FinalPractice results and suggest the grid lineup based on the fastest lap times.

The current price of drivers is not yet automated, will look into fetching it from F1 Grid

# how to get FP data

go to [sessions](https://api.openf1.org/v1/sessions?year=2024) and find circuit key

find [session keys](https://api.openf1.org/v1/sessions?year=2024&circuit_key=15)

find [laps data for each driver](https://api.openf1.org/v1/laps?session_key=9533&driver_number=81&is_pit_out_lap=false) and sort them by length

find [stint data for (fastest) lap](https://api.openf1.org/v1/stints?session_key=9533&driver_number=81&lap_start%3C=8&lap_end%3E=8) 

[API documentation](https://openf1.org/#introduction)