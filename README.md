# Escape the Sci

A first-person stealth game where you navigate a procedurally generated maze, complete teacher tasks, and avoid getting caught by the deans.

## How to Play

WASD - Move
Mouse - Look around
Shift - Run
E - Interact with teachers/objects

## Objective

Complete tasks for 2 teachers (Ms. Guig and Mr. Noody) to win. Avoid the deans - get caught 3 times and it's game over.

## Features

### Procedural Maze Generation
- Random 10x10 maze generated each playthrough
- Recursive backtracking algorithm
- Classrooms (green) and bathrooms (red) placed as safe zones

### Dean AI (Finite State Machine)
- Patrol - Walks between points, looking for player
- Chase - Runs directly at player when spotted
- Stalk - Activates after 2 detentions, uses prediction

### Bigram Prediction Model
The deans learn your movement patterns. They track which cells you visit and predict where you'll go next. The longer you play, the smarter they get.

### Vision System
Deans have a cone of vision. They can only see you if:
- You're within range
- You're within their view angle
- No walls are blocking

### Safe Zones
- Classrooms and bathrooms are safe
- Deans can't catch you inside rooms
- They'll wait outside for you

### Bell System
- Bell rings periodically
- Students follow/disperse on bell rings

## Scripts Overview

PlayerController - Movement, camera, interaction
DeanController - Enemy AI with FSM and prediction
GameManager - Game state, detentions, win/lose
MazeGenerator - Procedural maze creation
TeacherController - NPC tasks and quizzes
StudentController - Crowd behavior
Basketball - Shot meter mini-game

## Win Condition
Complete 2 teacher tasks

## Lose Condition
Get caught by deans 3 times

## Built With
- Unity 2022.3
- C#
- NavMesh for pathfinding
