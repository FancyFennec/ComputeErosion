# ComputeErosion
This is an implementation an erosion simulation in Unity based on the papers [Fast Hydraulic Erosion Simulation and Visualization on GPU](https://hal.inria.fr/inria-00402079/document) and 
[Interactive Terrain Modeling Using Hydraulic Erosion](https://cgg.mff.cuni.cz/~jaroslav/papers/2008-sca-erosim/2008-sca-erosiom-fin.pdf).

<a href="http://www.youtube.com/watch?feature=player_embedded&v=h7-jla5lwjQ
" target="_blank"><img src="http://img.youtube.com/vi/h7-jla5lwjQ/0.jpg" 
alt="Unity Erosion Simulation" width="580" height="360" border="10" /></a>

The simulation uses multiple compute shaders that compute the flow of water, sediment transport and decomposition and water evaporation.
# Goal of the project
The goal of this project was to get familiar with compute shaders in Unity and create beautiful pictures.
# How to use the Simulation
- Create a plane mesh with lots of subdivisions and import it into Unity (I just used Blender).
- Add an ErodingTerrain component.
- Add the VertexDisplacement material to its Mesh Renderer.
- Use the editor script to create a base terrain.
- Uncheck the 'EditTerrain' flag to run the simulation.
- Change the parameters to your liking.
- Check the 'EditTerrain' flag to edit the base terrain again.
- Rinse and repeat.
