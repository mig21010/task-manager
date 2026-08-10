import { useEffect, useState } from "react";

interface Task {
  id: number;
  title: string;
  description: string;
  isCompleted: boolean;
  cretedAt: string;
}

interface Props {
  filter: 'all' | 'completed' | 'pending';
}

const API_URL = "http://localhost:5183/api/Tasks";

export default function TaskList({ filter }: Props) {
    const [tasks, setTasks] = useState<Task[]>([]);
    const [loading, setLoading] = useState(true);

    const filteredTasks = tasks.filter(task => {
        if (filter === 'completed') return task.isCompleted;
        if (filter === 'pending') return !task.isCompleted;
        return true; // for 'all'
    });


    const fetchTasks = async () => {
        try {
            const response = await fetch(API_URL);
            if (!response.ok) {
                throw new Error("Failed to fetch tasks");
            }
            const data = await response.json();
            setTasks(data);
            setLoading(false);
        }
        catch (error) {
            console.error("Error fetching tasks:", error);
        }

    }

    useEffect(() => {
        fetchTasks();
    }, []);

    const handleDelete = async (id: number) => {
        try {
            const response = await fetch(`${API_URL}/${id}`, { method: 'DELETE' });
            if (!response.ok) {
                throw new Error("Failed to delete task");
            }
            // Remove the deleted task from the list
            setTasks(tasks.filter(task => task.id !== id));
        }
        catch (error) {
            console.error("Error deleting task:", error);
        }
    };

    const handleToggle = async (task: Task) => {
        try {
            const response = await fetch(`${API_URL}/${task.id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ ...task, isCompleted: !task.isCompleted })
            });
            if (!response.ok) {
                throw new Error("Failed to update task");
            }
            // Update the task in the list
            setTasks(tasks.map(t => t.id === task.id ? { ...t, isCompleted: !t.isCompleted } : t));
        }
        catch (error) {
            console.error("Error updating task:", error);
        }
    };


    if (loading) return (
        <p className="text-gray-500">Cargando tareas...</p>
    )

    return (
         <div className="space-y-3">
            {filteredTasks.map(task => (
                <div
                    key={task.id}
                    className="bg-white rounded-lg shadow p-4 flex items-start justify-between"
                >
                    <div className="flex items-start gap-3">
                        <input
                        type="checkbox"
                        checked={task.isCompleted}
                        onChange={() => handleToggle(task)}
                        className="mt-1 cursor-pointer"
                        />
                        <div>
                            
                        <h3 className="font-semibold text-gray-800">
                            {task.title}
                        </h3>
                        <p className="text-gray-500 text-sm">
                            {task.description}
                        </p>
                        <span className={`text-xs px-2 py-1 rounded
                            ${task.isCompleted
                            ? 'bg-green-100 text-green-700'
                            : 'bg-yellow-100 text-yellow-700'
                            }`}>
                            {task.isCompleted ? 'Completada' : 'Pendiente'}
                        </span>
                        </div>
                    </div>
                    <button
                        onClick={() => handleDelete(task.id)}
                        className="text-red-400 hover:text-red-600 text-sm ml-4"
                    >
                        Eliminar
                    </button>
                </div>
            ))}
        </div>
    )
}