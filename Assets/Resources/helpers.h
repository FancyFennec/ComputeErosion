bool isInside(uint3 id, int resolution);

//bool isInside(int i, int j, uint3 id, int resolution)
//{
//	return id.x + i >= 0 && id.y + j >= 0 && id.x + i < resolution - 1 && id.y + j < resolution - 1;
//}

bool isInside(uint3 id, int resolution)
{
	return id.x > 0 && id.y > 0 && id.x < resolution - 1 && id.y < resolution - 1;
}