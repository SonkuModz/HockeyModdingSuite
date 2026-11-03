using System;

namespace VGAudio.Utilities;

public static class Arrays
{
	public static T[] Generate<T>(int count, Func<int, T> elementGenerator)
	{
		T[] array = new T[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = elementGenerator(i);
		}
		return array;
	}

	public static void GetJaggedArrayInfo(Type type, out Type baseType, out int rank)
	{
		rank = -1;
		baseType = type;
		while ((object)type != null)
		{
			baseType = type;
			type = type.GetElementType();
			rank++;
		}
	}

	public static Type MakeJaggedArrayType(Type elementType, int rank)
	{
		Type type = elementType;
		for (int i = 0; i < rank; i++)
		{
			type = type.MakeArrayType();
		}
		return type;
	}
}
